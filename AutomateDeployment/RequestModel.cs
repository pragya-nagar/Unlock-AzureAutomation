
using System.Collections.Generic;
using AutomateDeployment.Models;

namespace AutomateDeployment
{
    public class RequestModel
    {
        public string UserEmailId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string OrganizationName { get; set; }
        public bool IsDomainByEmailId { get; set; }
        public bool IsRandomPassword { get; set; }
    }

    public class InnerRequestModel
    {
        public string SubDomain { get; set; }
        public string HttpsSubDomain { get; set; }
        public string TenantId { get; set; }
        public string ConnectionAdmin { get; set; }
        public string ConnectionOkr { get; set; }
        public string ConnectionFeedback { get; set; }
        public string ConnectionNotification { get; set; }
        public string ConnectionGuidedTour { get; set; }
        public List<FunctionDb> FunctionDb { get; set; }        
        public string ConnectionString { get; set; }
        public string UserAdEmailId { get; set; }

    }
    public class CredentialsModel
    {
        public string Environment { get; set; }
        public string ClientId { get; set; }
        public string ClientSecretId { get; set; }
        public string ObjectId { get; set; }
        public string TenantId { get; set; }
        public string KeyVaultUrl { get; set; }
        public string Domain { get; set; }
        public string CNameRecord { get; set; }
        public string SubscriptionId { get; set; }
        public string ResourceGroupName { get; set; }
        public string TTL { get; set; }
        public string TenantConnectionString { get; set; }
        public string OkrTrialConnectionString { get; set; }
        public string DemoExpDays { get; set; }
        public string StorageName { get; set; }
        public string StorageKey { get; set; }
        public string DBServerPassword { get; set; }
        public string DBServerName { get; set; }
        public string DBServerUserId { get; set; }
        public string ElasticPool { get; set; }
        public string BlobCdnUrl { get; set; }


    }


    public class AdUserResponse
    {
        public bool IsExist { get; set; }
        public string EmailId { get; set; }
        public string Id { get; set; }
    }
}
