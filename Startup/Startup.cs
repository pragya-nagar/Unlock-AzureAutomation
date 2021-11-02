using System;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using Microsoft.Extensions.Logging;

[assembly: FunctionsStartup(typeof(AutomateDeployment.Startup))]

namespace AutomateDeployment
{
    public class Startup : FunctionsStartup
    {
        // Override ConfigureAppConfiguration method to inject appsettings json files
        public override void ConfigureAppConfiguration(IFunctionsConfigurationBuilder builder)
        {
            FunctionsHostBuilderContext context = builder.GetContext();
            builder.ConfigurationBuilder
                .AddJsonFile(Path.Combine(context.ApplicationRootPath, "appsettings.json"), optional: true, reloadOnChange: false)
                .AddJsonFile(Path.Combine(context.ApplicationRootPath, $"appsettings.{context.EnvironmentName}.json"), optional: true, reloadOnChange: false)
                .AddEnvironmentVariables();
        }

        public override void Configure(IFunctionsHostBuilder builder)
        {
            // Inject Settings as Options
            builder.Services.AddOptions<AppSettingsConfigurationOptions>()
                .Configure<IConfiguration>((settings, configuration) => { configuration.Bind("Configurations", settings); });
        }
    }
}