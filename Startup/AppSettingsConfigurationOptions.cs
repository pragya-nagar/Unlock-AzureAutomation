namespace AutomateDeployment
{
    public class AppSettingsConfigurationOptions
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
        public int TTL { get; set; }
        public string TenantConnectionString { get; set; }
        public string InviteMessage { get; set; }
        public string DomainCreationMessage { get; set; }
        public SMTPDetails SMTPDetails { get; set; }
    }

    public class SMTPDetails
    {
        public string AWSEmailID { get; set; }
        public string AccountName { get; set; }
        public string Password { get; set; }
        public string ServerName { get; set; }
        public string Port { get; set; }
        public string IsSSLEnabled { get; set; }
        public string Host { get; set; }

    }
}
