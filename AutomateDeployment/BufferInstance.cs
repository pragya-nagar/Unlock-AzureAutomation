using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace AutomateDeployment
{
    public class BufferInstance
    {
        public async Task<IActionResult> BufferDatabaseCreation(CredentialsModel credentialsModel, ILogger log)
        {
            try
            {
                log.LogInformation("Buffer Database Creation Start");
                InnerRequestModel innerRequestModel = new InnerRequestModel();               

                if (!await IsDbExists(credentialsModel))
                {
                    await Common.BufferTenantCreation(innerRequestModel, log, credentialsModel);
                    for (int i = 0; i < innerRequestModel.FunctionDb.Count; i++)
                    {
                        innerRequestModel.FunctionDb[i].DBName = innerRequestModel.FunctionDb[i].DBName + "_" +innerRequestModel.TenantId;
                        await Common.DatabaseCreation(log, credentialsModel, innerRequestModel.FunctionDb[i]);
                        await Common.KeyVaultConnectionCreation(log, innerRequestModel, innerRequestModel.FunctionDb[i], credentialsModel);
                    }
                    await Common.ContainerCreation(log, innerRequestModel, credentialsModel);
                }

            }
            catch (Exception e)
            {
                log.LogInformation("Buffer Database Creation Error - " + e.StackTrace + e.Message);
            }
            return new OkObjectResult("Buffer Database Creation Done - Ok");
        }

        private async Task<bool> IsDbExists(CredentialsModel credentialsModel)
        {
            var dbExists = false;
            await using (var connection = new SqlConnection(credentialsModel.TenantConnectionString))
            {
                connection.Open();
                var query = $"SELECT COUNT(TenantId) FROM TenantMaster WHERE SubDomain = '' AND IsActive = 1";
                await using var command = new SqlCommand(query, connection)
                {
                    CommandTimeout = 180
                };
                await using var reader = await command.ExecuteReaderAsync();
                if (reader != null && reader.ReadAsync().Result)
                {
                    var dbCount = Convert.ToInt32(reader.GetValue(0));
                    if (dbCount >= 5)
                        dbExists = true;
                }

            }

            return dbExists;
        }        

    }
}
