using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Management.Dns;
using Microsoft.Azure.Management.Dns.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Rest.Azure.Authentication;
using System;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ExecutionContext = Microsoft.Azure.WebJobs.ExecutionContext;
using IActionResult = Microsoft.AspNetCore.Mvc.IActionResult;
using System.Collections.Generic;
using static AutomateDeployment.Common;
using AutomateDeployment.Models;
using Dapper;
using MimeKit;
using MailKit.Net.Smtp;

namespace AutomateDeployment
{
    public class Deployment
    {

        #region Azure Function

        [FunctionName("AutomationProcess")]
        [return: Queue("processed-trials", Connection = "AzureWebJobsStorage")]
        public async Task<IActionResult> AutomationProcess([QueueTrigger("requested-trials", Connection = "AzureWebJobsStorage")] string req, ILogger log, ExecutionContext executionContext)
        {
            var requestModel = JsonConvert.DeserializeObject<RequestModel>(req);
            var innerRequestModel = new InnerRequestModel();
            var responseMessage = $"Hello, Function triggered for queued email- {requestModel.UserEmailId}.  - AutomationProcess executed successfully.";

            try
            {


                var credentialsModel = Common.GetEnvironmentVariable(log);
                //var credentialsModel = Common.GetEnvironmentVariable(log, "Dev");

                log.LogInformation(" CreateSubDomain Start ");
                await CreateSubDomain(requestModel, innerRequestModel, log, credentialsModel);
                log.LogInformation(" CreateSubDomain End ");

                log.LogInformation(" ExecuteTenantDbScript Start ");
                await ExecuteTenantDbScript(requestModel, innerRequestModel, log, credentialsModel);
                log.LogInformation(" ExecuteTenantDbScript End ");

                log.LogInformation(" Parallel Execution Of AppRegistration Start ");
                await ParallelExecution(requestModel, log, innerRequestModel, credentialsModel);
                log.LogInformation(" Parallel Execution Of AppRegistration End ");

                log.LogInformation("Buffer Database Creation Start ");
                var bufferDatabaseObj = new BufferInstance();
                await bufferDatabaseObj.BufferDatabaseCreation(credentialsModel, log);
                log.LogInformation("Buffer Database Creation End ");

            }
            catch (Exception e)
            {
                log.LogError("Automation Process Error - " + e.Message);
            }
            return new OkObjectResult(responseMessage);
        }

        [FunctionName("AutomationBufferDBCreation")]
        [return: Queue("processed-trials-bufferdb", Connection = "AzureWebJobsStorage")]
        public async Task<IActionResult> AutomationBufferDbCreation([QueueTrigger("requested-trials-bufferdb", Connection = "AzureWebJobsStorage")] string req, ILogger log, ExecutionContext executionContext)
        {
            try
            {
                var bufferDatabaseObj = new BufferInstance();
                var credentialsModel = GetEnvironmentVariable(log);
                //var credentialsModel = Common.GetEnvironmentVariable(log, "Dev");                            

                log.LogInformation(" DatabaseCreation Start ");
                await bufferDatabaseObj.BufferDatabaseCreation(credentialsModel, log);
                log.LogInformation(" DatabaseCreation End ");

            }
            catch (Exception e)
            {
                log.LogError("Automation Process Error - " + e.Message);
            }
            return new OkObjectResult("Ok");
        }

        #endregion

        #region Public Method

        public async Task<RecordSet> CreateSubDomain(RequestModel requestModel, InnerRequestModel innerRequestModel, ILogger log, CredentialsModel credentialsModel)
        {

            log.LogInformation("Sub domain creation started for email {0}", requestModel.UserEmailId);
            var serviceCredentials = await ApplicationTokenProvider.LoginSilentAsync(credentialsModel.TenantId, credentialsModel.ClientId, credentialsModel.ClientSecretId);
            var dnsClient = new DnsManagementClient(serviceCredentials) { SubscriptionId = credentialsModel.SubscriptionId };
            string recordSetName;
            if (requestModel.IsDomainByEmailId)
            {
                recordSetName = requestModel.UserEmailId.Split('@')[1].Split('.')[0];
            }
            else
            {
                recordSetName = requestModel.FirstName?.Trim() + requestModel.LastName?.Trim().Substring(0, 1);
            }

            innerRequestModel.UserAdEmailId = requestModel.UserEmailId.Split('@')[0].Trim() + Common.Domain;
            recordSetName = recordSetName.ToLower();
            var recordSet = new RecordSet();

            try
            {
                log.LogInformation("Getting record sets with name {0}...", recordSetName);
                var recordSets = 0;
                var subDomainName = recordSetName;
                while (true)
                {
                    var domainStatus = await HasDomainNameAsync(credentialsModel, recordSetName, dnsClient);
                    if (!domainStatus)
                    {
                        break;
                    }

                    recordSets++;
                    recordSetName = subDomainName + recordSets;
                }

                log.LogInformation("Creating DNS 'CNAME' record set with name '{0}'...", recordSetName);
                var recordSetParams = new RecordSet
                {
                    TTL = Convert.ToInt32(credentialsModel.TTL),
                    CnameRecord = new CnameRecord(credentialsModel.CNameRecord)
                };
                recordSet = await dnsClient.RecordSets.CreateOrUpdateAsync(credentialsModel.ResourceGroupName, credentialsModel.Domain, recordSetName, RecordType.CNAME, recordSetParams);
                log.LogInformation("Successfully created");
                innerRequestModel.HttpsSubDomain = "https://" + recordSet.Name + "." + credentialsModel.Domain;
                innerRequestModel.SubDomain = recordSet.Name;
            }
            catch (Exception e)
            {
                log.LogInformation("SubDomain - failed: {0}", e.Message);
            }
            return recordSet;
        }
        public async Task ExecuteTenantDbScript(RequestModel requestModel, InnerRequestModel innerRequestModel, ILogger log, CredentialsModel credentialsModel)
        {
            try
            {
                log.LogInformation("Start ExecuteDbScript for DB Name - Tenants");
                innerRequestModel.TenantId = await IsTenantExists(credentialsModel);

                if (!string.IsNullOrEmpty(innerRequestModel.TenantId))
                {
                    await using (var connection = new SqlConnection(credentialsModel.TenantConnectionString))
                    {
                        connection.Open();
                        var demoExpDate = DateTime.UtcNow.AddDays(Convert.ToInt32(credentialsModel.DemoExpDays));
                        var queryUpdateTenantMaster = $"UPDATE [TenantMaster] SET [SubDomain] = '" + innerRequestModel.HttpsSubDomain.Replace("https://", "") + "' ,[CreatedOn] = GETDATE() ,[DemoExpiryDate] = '" + demoExpDate + "' WHERE TenantId = '" + innerRequestModel.TenantId + "'";

                        await using (var commandTenantMaster = new SqlCommand(queryUpdateTenantMaster, connection))
                        {
                            commandTenantMaster.CommandTimeout = 180;
                            await commandTenantMaster.ExecuteNonQueryAsync();
                        }
                        var queryTenantUserDetails = $"INSERT INTO [TenantUserDetails] ([EmailId], [TenantId], [IsActive], [CreatedBy], [CreatedOn], [UpdatedBy], [UpdatedOn]) SELECT '" + innerRequestModel.UserAdEmailId + "', '" + innerRequestModel.TenantId + "', 1, -1, GETDATE(), null, null";
                        await using (var commandTenantUserDetails = new SqlCommand(queryTenantUserDetails, connection))
                        {
                            commandTenantUserDetails.CommandTimeout = 180;
                            await commandTenantUserDetails.ExecuteNonQueryAsync();
                        }
                    }
                    await using (var connection = new SqlConnection(credentialsModel.OkrTrialConnectionString))
                    {
                        connection.Open();
                        var queryUpdateOkrTrialDetail = $"UPDATE [TrialDetails] SET [MappedEmailId] = '" + innerRequestModel.UserAdEmailId + "', SUBDOMAIN = '" + innerRequestModel.HttpsSubDomain.Replace("https://", "") + "' WHERE EMAILID = '" + requestModel.UserEmailId + "' AND ISACTIVE = 1;";
                        await using (var commandokrTrial = new SqlCommand(queryUpdateOkrTrialDetail, connection))
                        {
                            commandokrTrial.CommandTimeout = 180;
                            await commandokrTrial.ExecuteNonQueryAsync();
                        }

                    }
                }
                else
                {
                    await BufferTenantCreation(innerRequestModel, log, credentialsModel, false);
                    for (int i = 0; i < innerRequestModel.FunctionDb.Count; i++)
                    {
                        innerRequestModel.FunctionDb[i].DBName = innerRequestModel.FunctionDb[i].DBName + "_" + innerRequestModel.TenantId;
                        await DatabaseCreation(log, credentialsModel, innerRequestModel.FunctionDb[i]);
                        await KeyVaultConnectionCreation(log, innerRequestModel, innerRequestModel.FunctionDb[i], credentialsModel);
                    }
                    await ContainerCreation(log, innerRequestModel, credentialsModel);

                    await using (var connection = new SqlConnection(credentialsModel.TenantConnectionString))
                    {
                        connection.Open();
                        var queryTenantUserDetails = $"INSERT INTO [TenantUserDetails] ([EmailId], [TenantId], [IsActive], [CreatedBy], [CreatedOn], [UpdatedBy], [UpdatedOn]) SELECT '" + innerRequestModel.UserAdEmailId + "', '" + innerRequestModel.TenantId + "', 1, -1, GETDATE(), null, null;";
                        await using (var commandTenantUserDetails = new SqlCommand(queryTenantUserDetails, connection))
                        {
                            commandTenantUserDetails.CommandTimeout = 180;
                            await commandTenantUserDetails.ExecuteNonQueryAsync();
                        }
                    }
                    await using (var connection = new SqlConnection(credentialsModel.OkrTrialConnectionString))
                    {
                        connection.Open();
                        var queryUpdateOkrTrialDetail = $"UPDATE [TrialDetails] SET [MappedEmailId] = '" + innerRequestModel.UserAdEmailId + "', SUBDOMAIN = '" + innerRequestModel.HttpsSubDomain.Replace("https://", "") + "' WHERE EMAILID = '" + requestModel.UserEmailId + "' AND ISACTIVE = 1;";
                        await using (var commandokrTrial = new SqlCommand(queryUpdateOkrTrialDetail, connection))
                        {
                            commandokrTrial.CommandTimeout = 180;
                            await commandokrTrial.ExecuteNonQueryAsync();
                        }

                    }


                }
                await ExecuteAdminUserScript(requestModel, log, credentialsModel, innerRequestModel);

                log.LogInformation("End ExecuteDbScript");
            }
            catch (Exception e)
            {
                log.LogInformation(e.Message);
            }
        }
        public async Task<IActionResult> AppRegistration(ILogger log, InnerRequestModel innerRequestModel, CredentialsModel credentialsModel)
        {
            log.LogInformation("AppRegistration: C# HTTP trigger function - AppRegistration processed a request.");
            try
            {
                var graphClient = Common.GetGraphServiceClient(credentialsModel);

                var application = await graphClient.Applications[credentialsModel.ObjectId].Request().GetAsync();

                var ListRedirectUris = application.Spa.RedirectUris.ToList();

                ListRedirectUris.Add(innerRequestModel.HttpsSubDomain + "/secretlogin");
                ListRedirectUris.Add(innerRequestModel.HttpsSubDomain + "/logout");

                var addRedirectinApp = new Application()
                {
                    Spa = new SpaApplication()
                    {
                        RedirectUris = ListRedirectUris
                    }
                };

                await graphClient.Applications[credentialsModel.ObjectId].Request().UpdateAsync(addRedirectinApp);

            }
            catch (Exception e)
            {
                log.LogInformation("App Registration Error -" + e.Message);
            }
            return new OkObjectResult("App Registration Done");
        }
        public async Task<IActionResult> AddAdUser(RequestModel requestModel, ILogger log, InnerRequestModel innerRequestModel, CredentialsModel credentialsModel)
        {
            log.LogInformation("C# HTTP trigger function - AddUser processed a request.");
            try
            {
                if (string.IsNullOrEmpty(requestModel.UserEmailId)) return new OkObjectResult("EmailId could not be blank");
                var graphClient = Common.GetGraphServiceClient(credentialsModel);
                var emailSplit = requestModel.UserEmailId.Split('@');

                string userName;
                string displayName;
                string firstName;
                if (emailSplit[0].Contains('.'))
                {
                    var nameSplit = emailSplit[0].Split('.');
                    firstName = nameSplit[0];
                    firstName = firstName.Substring(0, 1).ToUpper() + firstName.Substring(1);
                    var lastName = nameSplit[1];
                    lastName = lastName.Substring(0, 1).ToUpper() + lastName.Substring(1);
                    displayName = firstName + " " + lastName;
                    userName = firstName + " " + lastName + ",";
                }
                else
                {
                    firstName = emailSplit[0];
                    firstName = firstName.Substring(0, 1).ToUpper() + firstName.Substring(1);
                    var lastName = ",";
                    displayName = firstName;
                    userName = firstName + lastName;
                }
                string emailAddress = innerRequestModel.UserAdEmailId;
                var userdetails = await IsUserExistInAdAsync(credentialsModel, emailAddress);
                string password = await CreatePassword(requestModel.IsRandomPassword);
                if (!userdetails.IsExist)
                {
                    var user = new Microsoft.Graph.User
                    {
                        AccountEnabled = true,
                        DisplayName = displayName,
                        MailNickname = firstName.Trim(),
                        UserType = "Guest",
                        JobTitle = "Designation",
                        UserPrincipalName = emailAddress,
                        PasswordProfile = new PasswordProfile
                        {
                            ForceChangePasswordNextSignIn = false,
                            Password = password
                        }

                    };
                    await graphClient.Users.Request().AddAsync(user);

                }
                await SendEmailAsync(credentialsModel, userName, password, requestModel, log, innerRequestModel);

            }
            catch (Exception e)
            {
                log.LogInformation(" AddAdUser Error - " + e.Message);
            }
            return new OkObjectResult("AddAdUser Done");
        }
        public async Task<MailerTemplate> GetMailerTemplate(CredentialsModel credentialsModel, string templateCode)
        {

            var data = new MailerTemplate();
            var conn = new SqlConnection("Server=" + credentialsModel.DBServerName + ";Initial Catalog=Notification;Persist Security Info=False;User ID=" + credentialsModel.DBServerUserId + ";Password=" + credentialsModel.DBServerPassword + ";MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;");
            using (var connection = conn)
            {
                if (conn != null)
                {
                    data = await connection.QueryFirstAsync<MailerTemplate>("select * from MailerTemplate where templateCode = @templateCode ", new
                    {

                        TemplateCode = templateCode,
                        IsActive = 1

                    });
                }
            }
            return data;
        }
        public async Task<MailSetupConfig> IsMailExist(CredentialsModel credentialsModel, string emailId)
        {

            var data = new MailSetupConfig();
            var conn = new SqlConnection("Server=" + credentialsModel.DBServerName + ";Initial Catalog=Notification;Persist Security Info=False;User ID=" + credentialsModel.DBServerUserId + ";Password=" + credentialsModel.DBServerPassword + ";MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;");
            using (var connection = conn)
            {
                if (conn != null)
                {
                    data = connection.QueryFirst<MailSetupConfig>("select * from MailSetupConfig where AwsemailId = @emailId ", new
                    {
                        TemplateCode = emailId,
                        IsActive = 1

                    });
                }
            }
            return data;

        }
        public async Task<IEnumerable<Emails>> GetEmailAddress(CredentialsModel credentialsModel)
        {
            IEnumerable<Emails> data = null;
            var conn = new SqlConnection("Server=" + credentialsModel.DBServerName + ";Initial Catalog=Notification;Persist Security Info=False;User ID=" + credentialsModel.DBServerUserId + ";Password=" + credentialsModel.DBServerPassword + ";MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;");
            using (var connection = conn)
            {
                if (conn != null)
                {

                    data = await connection.QueryAsync<Emails>("select * from Emails");
                }
            }
            return data.ToList();

        }

        public async Task<bool> SendEmailAsync(CredentialsModel credentialsModel, string userName, string password, RequestModel requestModel, ILogger log, InnerRequestModel innerRequestModel)
        {
            log.LogInformation("SendEmailAsync: C# HTTP trigger function - SendEmailAsync processed a request :- Started.");

            var template = await GetMailerTemplate(credentialsModel, TemplateCodes.TRV.ToString());
            string body = template.Body;

            string httpsSubDomain = innerRequestModel.HttpsSubDomain;
            string domainEmailId = innerRequestModel.UserAdEmailId;
            string defaultPassword = password;


            body = body.Replace("topBar", credentialsModel.BlobCdnUrl + Common.TopBarImage)

                .Replace("<name>", userName.Replace(",", ""))
               .Replace("logo", credentialsModel.BlobCdnUrl + Logo)
                 .Replace("<url>", httpsSubDomain)
                    .Replace("srcFacebook", credentialsModel.BlobCdnUrl + FacebookImage)
                    .Replace("srcInstagram", credentialsModel.BlobCdnUrl + InstagramImage)
                                              .Replace("srcTwitter", credentialsModel.BlobCdnUrl + TwitterImage)
                                              .Replace("srcLinkedin", credentialsModel.BlobCdnUrl + LinkedinImage)
                                              .Replace("ijk", InstagramUrl).Replace("lk", LinkedInUrl)
                                              .Replace("fb", FacebookUrl).Replace("terp", TwitterUrl)
                    .Replace("<credentials>", credentialsModel.BlobCdnUrl + Common.credentials)
                      .Replace("domainurl", httpsSubDomain)
                         .Replace("<durl>", httpsSubDomain)

                      .Replace("<domainId>", domainEmailId)
                      .Replace("<password>", defaultPassword)
              .Replace("handshake", credentialsModel.BlobCdnUrl + Common.handshake);


            MailRequest mailRequest = new MailRequest();

            var updatedBody = body;

            mailRequest.Body = updatedBody;
            mailRequest.MailTo = requestModel.UserEmailId;
            mailRequest.Subject = template.Subject;

            await SentMailWithoutAuthenticationAsync(credentialsModel, log, mailRequest);


            return true;

        }
        public async Task<bool> SentMailWithoutAuthenticationAsync(CredentialsModel credentialsModel, ILogger log, MailRequest mailRequest)
        {
            bool IsMailSent = false;


            try
            {
                MimeMessage message = new MimeMessage();

                string aWSEmailId = Common.AwsEmailId;
                string account = Common.AccountName;
                string password = Common.Password;
                int port = Common.Port;

                string host = Common.Host;
                string environment = "LIVE";


                if (string.IsNullOrWhiteSpace(mailRequest.MailFrom) && mailRequest.MailFrom == "")
                {
                    MailboxAddress from = new MailboxAddress("UnlockOKR", aWSEmailId);
                    message.From.Add(from);
                }
                else
                {
                    var isMailExist = await IsMailExist(credentialsModel, mailRequest.MailFrom);
                    if (isMailExist != null)
                    {
                        MailboxAddress mailboxAddress = new MailboxAddress("User", mailRequest.MailFrom);
                        message.From.Add(mailboxAddress);
                    }
                }

                MailboxAddress From = new MailboxAddress("UnlockOKR", aWSEmailId);
                message.From.Add(From);


                if (environment != "LIVE")
                {
                    mailRequest.Subject = mailRequest.Subject + " - Azure " + environment + " This mail is for " + mailRequest.MailTo;

                    var emails = await GetEmailAddress(credentialsModel);
                    foreach (var address in emails)
                    {
                        var emailAddress = new MailboxAddress(address.FullName, address.EmailAddress);
                        message.To.Add(emailAddress);
                    }
                    MailboxAddress CC = new MailboxAddress("shiv.kumar@compunneldigital.com");
                    message.Bcc.Add(CC);
                }

                else if (environment == "LIVE")
                {
                    string[] strTolist = mailRequest.MailTo.Split(';');

                    foreach (var item in strTolist)
                    {
                        MailboxAddress mailto = new MailboxAddress(item);
                        message.To.Add(mailto);
                    }


                    if (mailRequest.Bcc != "")
                    {
                        string[] strbcclist = mailRequest.CC.Split(';');
                        foreach (var item in strbcclist)
                        {
                            MailboxAddress bcc = new MailboxAddress(item);
                            message.Bcc.Add(bcc);
                        }
                    }

                    if (mailRequest.CC != "")
                    {
                        string[] strCcList = mailRequest.CC.Split(';');
                        foreach (var item in strCcList)
                        {
                            MailboxAddress CC = new MailboxAddress(item);
                            message.Cc.Add(CC);
                        }
                    }
                }


                message.Subject = mailRequest.Subject;
                BodyBuilder bodyBuilder = new BodyBuilder();
                bodyBuilder.HtmlBody = mailRequest.Body;
                message.Body = bodyBuilder.ToMessageBody();

                if (message.Subject != "")
                {
                    SmtpClient client = new SmtpClient();
                    client.Connect(host, port, false);
                    client.Authenticate(account, password);
                    client.Send(message);
                    client.Disconnect(true);
                    client.Dispose();

                    IsMailSent = true;

                }

            }
            catch (Exception e)
            {
                IsMailSent = false;
                log.LogInformation("SentMailWithoutAuthenticationAsync Error - " + e.StackTrace + e.Message);
            }
            log.LogInformation("SendEmailAsync: C# HTTP trigger function - SendEmailAsync processed a request :- Completed.");

            return IsMailSent;
        }
        public async Task<IActionResult> ParallelExecution(RequestModel requestModel, ILogger log, InnerRequestModel innerRequestModel, CredentialsModel credentialsModel)
        {
            try
            {
                var tasks = new Task[2];
                tasks[0] = AppRegistration(log, innerRequestModel, credentialsModel);
                tasks[1] = AddAdUser(requestModel, log, innerRequestModel, credentialsModel);
                await Task.WhenAll(tasks);
            }
            catch (Exception e)
            {
                log.LogInformation("Database Creation Error - " + e.StackTrace + e.Message);
            }
            return new OkObjectResult("Database Creation Done - Ok");
        }

        #endregion

        #region Private Method

        private async Task<string> IsTenantExists(CredentialsModel credentialsModel)
        {
            var tenantId = "";
            await using (var connection = new SqlConnection(credentialsModel.TenantConnectionString))
            {
                connection.Open();
                var query = $"SELECT TOP 1(TenantId) FROM TenantMaster WHERE SubDomain = '' AND IsActive = 1";
                await using var command = new SqlCommand(query, connection)
                {
                    CommandTimeout = 180
                };
                await using var reader = await command.ExecuteReaderAsync();
                if (reader != null && reader.ReadAsync().Result)
                {
                    tenantId = Convert.ToString(reader.GetValue(0));
                }

            }

            return tenantId;
        }
        private async Task ExecuteAdminUserScript(RequestModel requestModel, ILogger log, CredentialsModel credentialsModel, InnerRequestModel innerRequestModel)
        {
            try
            {
                log.LogInformation("Start ExecuteAdminUserScript");
                var dbName = "User_Management_" + innerRequestModel.TenantId;
                var connectionString = "Server = " + credentialsModel.DBServerName + "; Initial Catalog = " + dbName + "; Persist Security Info = False; User ID = " + credentialsModel.DBServerUserId + "; Password = " + credentialsModel.DBServerPassword + "; MultipleActiveResultSets = False; Encrypt = True; TrustServerCertificate = False; Connection Timeout = 180";
                long adminEmpId = 1;
                await using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    var passwordSalt = Guid.NewGuid().ToString();
                    var password = Common.EncryptRijndael("abcd@1234", passwordSalt);
                    var emailSplit = requestModel.UserEmailId.Split('@');
                    string firstName;
                    string lastName;

                    if (emailSplit[0].Contains('.'))
                    {
                        var nameSplit = emailSplit[0].Split('.');
                        firstName = nameSplit[0];
                        firstName = firstName.Substring(0, 1).ToUpper() + firstName.Substring(1);
                        lastName = nameSplit[1];
                        lastName = lastName.Substring(0, 1).ToUpper() + lastName.Substring(1);
                    }
                    else
                    {
                        firstName = emailSplit[0];
                        firstName = firstName.Substring(0, 1).ToUpper() + firstName.Substring(1);
                        lastName = ".";
                    }
                    var orgName = requestModel.UserEmailId.Split('@')[1].Split('.')[0];
                    orgName = orgName.Substring(0, 1).ToUpper() + orgName.Substring(1);
                    if (!string.IsNullOrEmpty(requestModel.OrganizationName))
                    {
                        orgName = requestModel.OrganizationName;
                    }

                    var queryAdminUser = $"INSERT INTO EMPLOYEES ([EmployeeCode], [FirstName], LastName, Password, PasswordSalt, Designation, EmailId, ReportingTo, ImagePath, OrganisationId, IsActive, CreatedBy, CreatedOn, UpdatedBy, UpdatedOn, RoleId, ProfileImageFile, LoginFailCount) VALUES ('', '" + firstName + "', '" + lastName + "', '" + password + "', '" + passwordSalt + "', 'Principle Product Manager', '" + innerRequestModel.UserAdEmailId + "', 0,null, 1, 1, 0, GetDate(), 0,  GetDate(), 2 ,null, 0)";
                    var querySelectAdminUser = $"SELECT EMPLOYEEID FROM EMPLOYEES WHERE EMAILID = '" + innerRequestModel.UserAdEmailId + "' AND ISACTIVE = 1 AND FIRSTNAME = '" + firstName + "' AND LASTNAME = '" + lastName + "'";


                    await using (var commandUserAdmin = new SqlCommand(queryAdminUser, connection))
                    {
                        commandUserAdmin.CommandTimeout = 180;
                        await commandUserAdmin.ExecuteNonQueryAsync();
                    }
                    await using (var getCommand = new SqlCommand(querySelectAdminUser, connection))
                    {
                        getCommand.CommandTimeout = 180;
                        await using var reader = await getCommand.ExecuteReaderAsync();
                        if (reader != null && reader.ReadAsync().Result)
                        {
                            adminEmpId = Convert.ToInt64(reader.GetValue(0));
                        }
                    }

                    var queryUpdateOrg = $"UPDATE Organisations SET OrganisationName = '" + orgName + "', OrganisationHead = '" + adminEmpId + "' WHERE OrganisationId = 1";
                    await using (var commandUpdateOrg = new SqlCommand(queryUpdateOrg, connection))
                    {
                        commandUpdateOrg.CommandTimeout = 180;
                        await commandUpdateOrg.ExecuteNonQueryAsync();
                    }
                    //Add new code updated ReportTo Updated
                    var queryReportingTo = $"UPDATE [EMPLOYEES]  SET ReportingTo = " + adminEmpId + " Where EMPLOYEEID in(SELECT EMPLOYEEID FROM EMPLOYEES where  EMPLOYEEID not in (" + adminEmpId + "))";
                    await using (var commandReportingTo = new SqlCommand(queryReportingTo, connection))
                    {
                        commandReportingTo.CommandTimeout = 180;
                        await commandReportingTo.ExecuteNonQueryAsync();
                    }
                }

                await ExecuteGuidedTourScript(log, credentialsModel, adminEmpId, innerRequestModel);

                log.LogInformation("End ExecuteAdminUserScript");
            }
            catch (Exception e)
            {
                log.LogInformation(e.Message);
            }
        }
        private async Task ExecuteGuidedTourScript(ILogger log, CredentialsModel credentialsModel, long adminEmpId, InnerRequestModel innerRequestModel)
        {
            try
            {
                log.LogInformation("Start ExecuteGuidedTourScript");
                var dbName = "Guided_Tour_" + innerRequestModel.TenantId;
                var connectionString = "Server = " + credentialsModel.DBServerName + "; Initial Catalog = " + dbName + "; Persist Security Info = False; User ID = " + credentialsModel.DBServerUserId + "; Password = " + credentialsModel.DBServerPassword + "; MultipleActiveResultSets = False; Encrypt = True; TrustServerCertificate = False; Connection Timeout = 60";
                await using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    var queryGuidedTour = $"Insert into OnBoardingControl (employeeId,skipCount,ReadyCount,CreatedBy,CreatedOn,UpdatedBy,updatedOn,isActive)Values(" + adminEmpId + ",0,0,1,getdate(),null,null,1)";
                    await using (var commandGuidedTour = new SqlCommand(queryGuidedTour, connection))
                    {
                        commandGuidedTour.CommandTimeout = 180;
                        await commandGuidedTour.ExecuteNonQueryAsync();
                    }
                }

                log.LogInformation("End ExecuteGuidedTourScript");
            }
            catch (Exception e)
            {
                log.LogInformation(e.Message);
            }
        }
        private async Task<bool> HasDomainNameAsync(CredentialsModel credentialsModel, string subDomain, DnsManagementClient dnsClient)
        {
            var page = await dnsClient.RecordSets.ListAllByDnsZoneAsync(credentialsModel.ResourceGroupName, credentialsModel.Domain);
            while (true)
            {
                if (page.Any(x => x.Type == "Microsoft.Network/dnszones/CNAME" && string.Equals(x.Name, subDomain, StringComparison.CurrentCultureIgnoreCase)))
                {
                    return true;
                }
                if (string.IsNullOrEmpty(page.NextPageLink))
                {
                    break;
                }
                page = await dnsClient.RecordSets.ListAllByDnsZoneNextAsync(page.NextPageLink);
            }
            return false;
        }
        private async Task<string> CreatePassword(bool isRandomPassword)
        {
            string password = isRandomPassword ? Common.RandomPassword : Common.DefaultPassword;
            if (isRandomPassword)
            {
                Random _random = new Random();
                int number = await Task.Run(() => _random.Next(100, 999));
                password = password + number;
            }
            return password;
        }
        private async Task<AdUserResponse> IsUserExistInAdAsync(CredentialsModel credentialsModel, string username)
        {

            var response = new AdUserResponse();
            var graphClient = Common.GetGraphServiceClient(credentialsModel);

            var graphResponse = await graphClient.Users.Request().Filter("mail eq '" + username + "' or userPrincipalName eq '" + username + "'").GetAsync();
            response.IsExist = graphResponse.Count > 0;
            response.EmailId = username;
            if (graphResponse.Count > 0)
                response.Id = graphResponse.FirstOrDefault()?.Id;
            return response;

        }

        #endregion

    }

}
