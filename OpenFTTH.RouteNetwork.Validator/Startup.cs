using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenFTTH.RouteNetwork.Validator.Config;
using OpenFTTH.RouteNetwork.Validator.Consumers;
using OpenFTTH.RouteNetwork.Validator.Consumers.Kafka;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenFTTH.RouteNetwork.Validator
{
    public class Startup
    {
        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            var hostBuilder = new HostBuilder();

            ConfigureApp(hostBuilder);
            ConfigureLogging(hostBuilder);
            ConfigureServices(hostBuilder);

            return hostBuilder;
        }

        private static void ConfigureApp(IHostBuilder hostBuilder)
        {
            hostBuilder.ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.AddEnvironmentVariables();
                config.AddJsonFile("appsettings.json", true, true);
            });
        }

        private static void ConfigureLogging(IHostBuilder hostBuilder)
        {
            Log.Logger = new LoggerConfiguration()
             .Enrich.FromLogContext()
             .WriteTo.Console()
             .WriteTo.Debug()
             .CreateLogger();

            hostBuilder.ConfigureServices((hostContext, services) =>
            {
                services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));
            });
        }

        public static void ConfigureServices(IHostBuilder hostBuilder)
        {
            hostBuilder.ConfigureServices((hostContext, services) =>
            {
                services.AddOptions();

                services.Configure<KafkaSetting>(kafkaSettings =>
                                                hostContext.Configuration.GetSection("Kafka").Bind(kafkaSettings));

                services.Configure<DatabaseSetting>(databaseSettings =>
                                               hostContext.Configuration.GetSection("Database").Bind(databaseSettings));

                services.AddLogging();
                services.AddSingleton<IGenericEventDispatcher, KafkaEventDispatcher>();
                services.AddHostedService<Worker>();
            });
        }
    }
}
