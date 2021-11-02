using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Auth;
using Microsoft.Identity.Client;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using System.Data.SqlClient;
using Dapper;
using AutomateDeployment.Models;
using System.Linq;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;

namespace AutomateDeployment
{
    public static class Common
    {
        public static string InviteMessage = "Dear <user>\n\n You are invited to join Unlock OKR software. It is integrated and intelligent platform to collaborate and achieve organizations OKRs. You will be delighted to explore and adopt OKRs by leveraging world class technology that is as powerful as it is intuitive. \n\n Click on the below accept invitation button to join the platform.";
        //public static string Domain = "@msdnincsysinc.onmicrosoft.com";
        public static string Domain = "@unlockokr.online";
       
        public static string DefaultPassword = "H@ppy100";
        public static string RandomPassword = "H@ppy";

        public const string AwsEmailId = "adminsupport@unlockokr.com";

        public const string AccountName = "AKIAJVT7R6HES36CNLWQ";
        public const string Password = "AmbzlYKroTfzrc2+tXUTXYcO55HBd0EfOn1rheEma6Kp";
        public const int Port = 587;
        public const string Host = "email-smtp.us-east-1.amazonaws.com";
        public const string Environment = "Dev";
        public const bool IsSSLEnabled = false;


        public const string TopBarImage = "topBar.png";
        public const string TickImage = "tick.png";
        public const string Logo = "logo.png";
        public const string LoginButtonImage = "login.png";
        public const string LinkedinImage = "linkedin.png";
        public const string FacebookImage = "facebook.png";
        public const string InstagramImage = "instagram.png";
        public const string TwitterImage = "twitter.png";
        public const string FooterImage = "footer-logo.png";
        public const string credentials = "credentials.png";
        public const string handshake = "hand-shake.png";



        public const string FacebookUrl = "https://www.facebook.com/unlockokr";
        public const string TwitterUrl = "https://twitter.com/unlockokr";
        public const string LinkedInUrl = "https://www.linkedin.com/company/unlock-okr";
        public const string InstagramUrl = "https://www.instagram.com/unlockokr";
        public const string TermsOfUseUrl = "https://okr-dev-v2.compunnel.com/terms-of-use";
        public const string PrivacyPolicyUrl = "https://okr-dev-v2.compunnel.com/privacy-policy";

        public enum TemplateCodes
        {
            TRV = 1
        }

        public static async Task<string> GetBlobData(CredentialsModel credentialsModel, string fileName, ILogger log)
        {
            var content = string.Empty;
            try
            {
                var account = new CloudStorageAccount(new StorageCredentials(credentialsModel.StorageName, credentialsModel.StorageKey), true);
                var cloudBlobClient = account.CreateCloudBlobClient();

                var cloudBlobContainer = cloudBlobClient.GetContainerReference("common");
                var container = cloudBlobContainer.GetBlockBlobReference(fileName);
                content = await container.DownloadTextAsync();
            }
            catch (Exception e)
            {

                log.LogInformation(e.StackTrace);
            }

            return content;
        }
        public static string EncryptRijndael(string input, string salt)
        {
            if (string.IsNullOrEmpty(input))
                throw new ArgumentNullException("input");
            var aesAlg = NewRijndaelManaged(salt);
            var encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
            var msEncrypt = new MemoryStream();
            using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
            using (var swEncrypt = new StreamWriter(csEncrypt))
            {
                swEncrypt.Write(input);
            }
            return Convert.ToBase64String(msEncrypt.ToArray());
        }        
        public static CredentialsModel GetEnvironmentVariable(ILogger log)
        {
            var credentialsModel = new CredentialsModel
            {
                ClientId = System.Environment.GetEnvironmentVariable("ClientId", EnvironmentVariableTarget.Process),
                ClientSecretId = System.Environment.GetEnvironmentVariable("ClientSecretId", EnvironmentVariableTarget.Process),
                CNameRecord = System.Environment.GetEnvironmentVariable("CNameRecord", EnvironmentVariableTarget.Process),
                Domain = System.Environment.GetEnvironmentVariable("Domain", EnvironmentVariableTarget.Process),
                KeyVaultUrl = System.Environment.GetEnvironmentVariable("KeyVaultUrl", EnvironmentVariableTarget.Process),
                ObjectId = System.Environment.GetEnvironmentVariable("ObjectId", EnvironmentVariableTarget.Process),
                ResourceGroupName = System.Environment.GetEnvironmentVariable("ResourceGroupName", EnvironmentVariableTarget.Process),
                SubscriptionId = System.Environment.GetEnvironmentVariable("SubscriptionId", EnvironmentVariableTarget.Process),
                TenantConnectionString = System.Environment.GetEnvironmentVariable("TenantConnectionString", EnvironmentVariableTarget.Process),
                TenantId = System.Environment.GetEnvironmentVariable("TenantId", EnvironmentVariableTarget.Process),
                TTL = System.Environment.GetEnvironmentVariable("TTL", EnvironmentVariableTarget.Process),
                StorageName = System.Environment.GetEnvironmentVariable("StorageName", EnvironmentVariableTarget.Process),
                StorageKey = System.Environment.GetEnvironmentVariable("StorageKey", EnvironmentVariableTarget.Process),
                DemoExpDays = System.Environment.GetEnvironmentVariable("DemoExpDays", EnvironmentVariableTarget.Process),
                DBServerPassword = System.Environment.GetEnvironmentVariable("DBServerPassword", EnvironmentVariableTarget.Process),
                DBServerName = System.Environment.GetEnvironmentVariable("DBServerName", EnvironmentVariableTarget.Process),
                DBServerUserId = System.Environment.GetEnvironmentVariable("DBServerUserId", EnvironmentVariableTarget.Process),
                OkrTrialConnectionString = System.Environment.GetEnvironmentVariable("OkrTrialConnectionString", EnvironmentVariableTarget.Process),
                ElasticPool = System.Environment.GetEnvironmentVariable("ElasticPool", EnvironmentVariableTarget.Process),
                BlobCdnUrl = System.Environment.GetEnvironmentVariable("BlobCdnUrl", EnvironmentVariableTarget.Process)
            };
            log.LogInformation("TenantID - " + credentialsModel.TenantId);
            log.LogInformation("ClientID - " + credentialsModel.ClientId);
            log.LogInformation("ClientSecretID - " + credentialsModel.ClientSecretId);
            log.LogInformation("CNameRecord - " + credentialsModel.CNameRecord);
            log.LogInformation("Domain - " + credentialsModel.Domain);
            log.LogInformation("KeyVaultUrl - " + credentialsModel.KeyVaultUrl);
            log.LogInformation("ObjectId - " + credentialsModel.ObjectId);
            log.LogInformation("ResourceGroupName - " + credentialsModel.ResourceGroupName);
            log.LogInformation("StorageKey - " + credentialsModel.StorageKey);
            log.LogInformation("StorageName - " + credentialsModel.StorageName);
            log.LogInformation("SubscriptionId - " + credentialsModel.SubscriptionId);
            log.LogInformation("TenantConnectionString - " + credentialsModel.TenantConnectionString);
            log.LogInformation("TTL - " + credentialsModel.TTL);
            return credentialsModel;
        }
        public static CredentialsModel GetEnvironmentVariable(ILogger log, string environment)
        {
            if (environment == "Dev")
            {
                var credentialsModel = new CredentialsModel
                {

                    ClientId = "ad2e6351-5e26-46e7-90bc-e449934f43e2",
                    ClientSecretId = "Ph_qb4deK3Bm.~0_4-JsPsR6p~~Dw56q-l",
                    CNameRecord = "unlockokr-ui-dev.azurewebsites.net",
                    Domain = "unlockokr.com",
                    KeyVaultUrl = "https://unlockokr-vault-dev.vault.azure.net/",
                    ObjectId = "8858d976-ca69-4f74-a266-a07f41e2e323",
                    ResourceGroupName = "unlockokr-prod",
                    SubscriptionId = "a8b508b6-da16-4c45-84f5-cac5c9f57513",
                    TenantConnectionString = "Server=unlockokr-db-dev.database.windows.net;Initial Catalog= Tenants;Persist Security Info=False;User ID=unlockokr-db-dev-admin;Password=fbBfXENoi2WCACXK;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;",
                    TenantId = "e648628f-f65c-40cc-8a28-c601daf26a89",
                    TTL = "60",
                    StorageName = "unlockokrblobdev",
                    StorageKey = "oXP92O+N3SRxHT3GHUT0uhppDmbVjQYwJUB7pOY1bYdxjCV93JH/FbIaWE/hNT6obd+T9vxYGeBLkKT/DZZkow==",
                    DemoExpDays = "14",
                    OkrTrialConnectionString = "Server=unlockokr-db-dev.database.windows.net;Initial Catalog= OKR_Trial;Persist Security Info=False;User ID=unlockokr-db-dev-admin;Password=fbBfXENoi2WCACXK;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;",
                    DBServerPassword = "fbBfXENoi2WCACXK",
                    DBServerName = "unlockokr-db-dev.database.windows.net",
                    DBServerUserId = "unlockokr-db-dev-admin",
                    ElasticPool = "[unlockokr-db-pool-dev]",
                    BlobCdnUrl = "https://unlockokrblobcdndev.azureedge.net/common/"
                };
                return credentialsModel;
            }

            else if (environment == "Uat")
            {
                var credentialsModel = new CredentialsModel
                {

                    ClientId = "104c9960-69ce-4a95-b421-9c980135f62f",
                    ClientSecretId = "fm_8r7Dw779pnqG41.o.W~.VdWy4-hjJu4",
                    CNameRecord = "unlockokr-ui-uat.azurewebsites.net",
                    Domain = "unlockokr.com",
                    KeyVaultUrl = "https://unlockokr-vault-uat.vault.azure.net/",
                    ObjectId = "e9c15f74-4981-413e-9cc6-facfaf679a16",
                    ResourceGroupName = "unlockokr-prod",
                    SubscriptionId = "a8b508b6-da16-4c45-84f5-cac5c9f57513",
                    TenantConnectionString = "Server=unlockokr-db-uat.database.windows.net;Initial Catalog= Tenants;Persist Security Info=False;User ID=unlockokr-db-uat-admin;Password=L7ESdx9d51NASRfa;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;",
                    TenantId = "e648628f-f65c-40cc-8a28-c601daf26a89",
                    TTL = "60",
                    StorageName = "unlockokrblobuat",
                    StorageKey = "cnue+W3Bgarwzpqi2jS8G0zyBrJaJovCNsfQ7NK7GOxlzVZ2L7WsPgxMFHobs+a1qxUkz0BtJcCBFXxU4O6yww==",
                    DemoExpDays = "14",
                    OkrTrialConnectionString = "Server=unlockokr-db-uat.database.windows.net;Initial Catalog= OKR_Trial;Persist Security Info=False;User ID=unlockokr-db-uat-admin;Password=L7ESdx9d51NASRfa;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;",
                    DBServerPassword = "L7ESdx9d51NASRfa",
                    DBServerName = "unlockokr-db-uat.database.windows.net",
                    DBServerUserId = "unlockokr-db-uat-admin",
                    ElasticPool = "[unlockokr-db-pool-uat]",
                    BlobCdnUrl = "https://unlockokrcdnuat.azureedge.net/common"
                };
                return credentialsModel;
            }
            else
            {
                var credentialsModel = new CredentialsModel
                {

                    ClientId = "ad2e6351-5e26-46e7-90bc-e449934f43e2",
                    ClientSecretId = "Ph_qb4deK3Bm.~0_4-JsPsR6p~~Dw56q-l",
                    CNameRecord = "unlockokr-ui-dev.azurewebsites.net",
                    Domain = "unlockokr.com",
                    KeyVaultUrl = "https://unlockokr-vault-dev.vault.azure.net/",
                    ObjectId = "e9c15f74-4981-413e-9cc6-facfaf679a16",
                    ResourceGroupName = "unlockokr-prod",
                    SubscriptionId = "a8b508b6-da16-4c45-84f5-cac5c9f57513",
                    TenantConnectionString = "Server=unlockokr-db-uat.database.windows.net;Initial Catalog= Tenants;Persist Security Info=False;User ID=unlockokr-db-uat-admin;Password=L7ESdx9d51NASRfa;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;",
                    TenantId = "e648628f-f65c-40cc-8a28-c601daf26a89",
                    TTL = "60",
                    StorageName = "unlockokrblobdev",
                    StorageKey = "oXP92O+N3SRxHT3GHUT0uhppDmbVjQYwJUB7pOY1bYdxjCV93JH/FbIaWE/hNT6obd+T9vxYGeBLkKT/DZZkow==",
                    DemoExpDays = "14",
                    OkrTrialConnectionString = "Server=unlockokr-db-uat.database.windows.net;Initial Catalog= OKR_Trial;Persist Security Info=False;User ID=unlockokr-db-uat-admin;Password=L7ESdx9d51NASRfa;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;",
                    DBServerPassword = "fbBfXENoi2WCACXK",
                    DBServerName = "unlockokr-db-dev.database.windows.net",
                    DBServerUserId = "unlockokr-db-dev-admin",
                    ElasticPool = "[unlockokr-db-pool-prod]",
                    BlobCdnUrl = "https://unlockokrblobcdnprod.azureedge.net/common/"
                };
                return credentialsModel;
            }


        }
        public static GraphServiceClient GetGraphServiceClient(CredentialsModel credentialsModel)
        {
            IConfidentialClientApplication confidentialClientApplication = ConfidentialClientApplicationBuilder
                .Create(credentialsModel.ClientId)
                .WithTenantId(credentialsModel.TenantId)
                .WithClientSecret(credentialsModel.ClientSecretId)
                .Build();

            ClientCredentialProvider authProvider = new ClientCredentialProvider(confidentialClientApplication);
            GraphServiceClient graphClient = new GraphServiceClient(authProvider);
            return graphClient;
        }
        public static async Task CreateDatabase(string dbName, CredentialsModel credentialsModel, ILogger log, SqlConnection connection)
        {
            log.LogInformation(dbName + "DB Creation Start");
            dbName = "[" + dbName + "]";
            var queryDb = $"CREATE DATABASE " + dbName + " (SERVICE_OBJECTIVE = ELASTIC_POOL (name = " + credentialsModel.ElasticPool + "));";
            var commandDb = new SqlCommand(queryDb, connection);
            commandDb.CommandTimeout = 300;
            await commandDb.ExecuteNonQueryAsync();
            log.LogInformation(dbName + "DB Creation End");
        }
        public static async Task BufferTenantCreation(InnerRequestModel innerRequestModel, ILogger log, CredentialsModel credentialsModel, bool isBuferTenant = true)
        {
            try
            {
                log.LogInformation("Start BufferTenantCreation");
                var tenantId = "";
                await using (var connection = new SqlConnection(credentialsModel.TenantConnectionString))
                {
                    connection.Open();
                    var demoExpDate = DateTime.UtcNow.AddDays(Convert.ToInt32(credentialsModel.DemoExpDays));
                    var queryTenantMaster = $"INSERT INTO [TenantMaster]([TenantId],[SubDomain],[IsActive],[CreatedBy],[CreatedOn],[UpdatedBy],[UpdatedOn],[IsLicensed],[DemoExpiryDate],[LicenseCreatedOn]) VALUES(NEWID(),'',1,-1,GETDATE(),null,null,0,null,null)";
                    var querySelectTenantMaster = $"SELECT TOP 1(TenantId) FROM TenantMaster WHERE SubDomain = '' AND IsActive = 1";
                    if (!isBuferTenant)
                    {
                        queryTenantMaster = $"INSERT INTO [TenantMaster]([TenantId],[SubDomain],[IsActive],[CreatedBy],[CreatedOn],[UpdatedBy],[UpdatedOn],[IsLicensed],[DemoExpiryDate],[LicenseCreatedOn]) VALUES(NEWID(),'" + innerRequestModel.HttpsSubDomain.Replace("https://", "") + "',1,-1,GETDATE(),null,null,0,'" + demoExpDate + "',null)";
                        querySelectTenantMaster = $"SELECT TOP 1(TenantId) FROM TenantMaster WHERE SubDomain = '" + innerRequestModel.HttpsSubDomain.Replace("https://", "") + "' AND IsActive = 1";
                    }
                    var queryFunctionDbList = $"SELECT DBName,ScriptName,ConnectionServiceName FROM FunctionDb WHERE IsActive = 1";

                    await using (var commandTenantMaster = new SqlCommand(queryTenantMaster, connection))
                    {
                        commandTenantMaster.CommandTimeout = 180;
                        await commandTenantMaster.ExecuteNonQueryAsync();
                    }
                    await using (var getCommand = new SqlCommand(querySelectTenantMaster, connection))
                    {
                        getCommand.CommandTimeout = 180;
                        await using var reader = await getCommand.ExecuteReaderAsync();
                        if (reader != null && reader.ReadAsync().Result)
                        {
                            tenantId = Convert.ToString(reader.GetValue(0));
                        }
                    }

                    var functionDbList = await connection.QueryAsync<FunctionDb>(queryFunctionDbList);
                    innerRequestModel.FunctionDb = functionDbList.ToList();

                }
                innerRequestModel.TenantId = tenantId;
                log.LogInformation("End ExecuteDbScript");
            }
            catch (Exception e)
            {
                log.LogInformation(e.Message);
            }
        }
        public static async Task KeyVaultConnectionCreation(ILogger log, InnerRequestModel innerRequestModel, FunctionDb functionDbRequestModel, CredentialsModel credentialsModel)
        {
            log.LogInformation("C# HTTP trigger function - KeyVaultSecret processed a request.");
            try
            {

                var credentials = new ClientSecretCredential(credentialsModel.TenantId, credentialsModel.ClientId, credentialsModel.ClientSecretId);
                var client = new SecretClient(new Uri(credentialsModel.KeyVaultUrl), credentials);
                var connectionString = @"Server = " + credentialsModel.DBServerName + "; Initial Catalog = " + functionDbRequestModel.DBName + "; Persist Security Info = False; User ID = " + credentialsModel.DBServerUserId + "; Password = " + credentialsModel.DBServerPassword + "; MultipleActiveResultSets = False; Encrypt = True; TrustServerCertificate = False; Connection Timeout = 60";
                var connectionStringName = innerRequestModel.TenantId + "-" + functionDbRequestModel.ConnectionServiceName;
                await client.SetSecretAsync(connectionStringName, connectionString);

            }
            catch (Exception e)
            {
                log.LogInformation("KeyVault Creation Error - " + e.Message);
            }
        }
        public static async Task ContainerCreation(ILogger log, InnerRequestModel innerRequestModel, CredentialsModel credentialsModel)
        {
            log.LogInformation("C# HTTP trigger function - ContainerCreation processed a request.");
            try
            {
                var account = new CloudStorageAccount(new StorageCredentials(credentialsModel.StorageName, credentialsModel.StorageKey), true);
                var cloudBlobClient = account.CreateCloudBlobClient();

                var cloudBlobContainer = cloudBlobClient.GetContainerReference(innerRequestModel.TenantId.ToLower());

                if (await cloudBlobContainer.CreateIfNotExistsAsync())
                {
                    await cloudBlobContainer.SetPermissionsAsync(
                        new BlobContainerPermissions
                        {
                            PublicAccess = BlobContainerPublicAccessType.Container

                        }
                    );
                }
            }
            catch (Exception e)
            {
                log.LogInformation(e.Message);
            }
        }
        public static async Task DatabaseCreation(ILogger log, CredentialsModel credentialsModel, FunctionDb functionDbRequestModel)
        {
            try
            {
                await using var connection = new SqlConnection("Server=" + credentialsModel.DBServerName + ";Initial Catalog=Master;Persist Security Info=False;User ID=" + credentialsModel.DBServerUserId + ";Password=" + credentialsModel.DBServerPassword + ";MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;");
                connection.Open();
                await CreateDatabase(functionDbRequestModel.DBName, credentialsModel, log, connection);
                await ExecuteDbScript(functionDbRequestModel.DBName, functionDbRequestModel.ScriptName, log, credentialsModel);

            }
            catch (Exception e)
            {
                log.LogInformation("Feedback Buffer Database Creation Error - " + e.StackTrace + e.Message);
            }
        }
        private static async Task ExecuteDbScript(string dbName, string dbScriptName, ILogger log, CredentialsModel credentialsModel)
        {
            try
            {
                log.LogInformation("Start ExecuteBufferDbScript for DB Name -" + dbName);

                var connectionString = @"Server = " + credentialsModel.DBServerName + "; Initial Catalog = " + dbName + "; Persist Security Info = False; User ID = " + credentialsModel.DBServerUserId + "; Password = " + credentialsModel.DBServerPassword + "; MultipleActiveResultSets = False; Encrypt = True; TrustServerCertificate = False; Connection Timeout = 180";

                var readLocalFile = await GetBlobData(credentialsModel, dbScriptName, log);

                await using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);

                var server = new Server(new ServerConnection(connection));

                await Task.Run(() =>
                {
                    server.ConnectionContext.ExecuteNonQuery(readLocalFile);
                });

                log.LogInformation("End ExecuteBufferDbScript for DB Name -" + dbName);
            }
            catch (Exception e)
            {

                log.LogError(e.StackTrace);
            }


        }
        private static RijndaelManaged NewRijndaelManaged(string salt)
        {
            string InputKey = "99334E81-342C-4900-86D9-07B7B9FE5EBB";
            if (salt == null) throw new ArgumentNullException("salt");
            var saltBytes = System.Text.Encoding.ASCII.GetBytes(salt);
            var key = new Rfc2898DeriveBytes(InputKey, saltBytes);

            var aesAlg = new RijndaelManaged();
            aesAlg.Key = key.GetBytes(aesAlg.KeySize / 8);
            aesAlg.IV = key.GetBytes(aesAlg.BlockSize / 8);
            return aesAlg;
        }
    }
}
