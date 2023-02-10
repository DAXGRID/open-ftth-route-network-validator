using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using OpenFTTH.EventSourcing;
using OpenFTTH.EventSourcing.Postgres;
using OpenFTTH.RouteNetwork.Validator.Config;
using OpenFTTH.RouteNetwork.Validator.Database.Impl;
using OpenFTTH.RouteNetwork.Validator.Notification;
using OpenFTTH.RouteNetwork.Validator.Projections;
using OpenFTTH.RouteNetwork.Validator.State;
using OpenFTTH.RouteNetwork.Validator.Validators;
using Serilog;
using Serilog.Formatting.Compact;
using System;

namespace OpenFTTH.RouteNetwork.Validator;

public class Startup
{
    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        var hostBuilder = new HostBuilder();

        ConfigureApp(hostBuilder);
        ConfigureSerialization(hostBuilder);
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

    private static void ConfigureSerialization(IHostBuilder hostBuilder)
    {
        JsonConvert.DefaultSettings = (() =>
        {
            var settings = new JsonSerializerSettings();
            settings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            settings.Converters.Add(new StringEnumConverter());
            settings.TypeNameHandling = TypeNameHandling.Auto;
            return settings;
        });
    }

    private static void ConfigureLogging(IHostBuilder hostBuilder)
    {
        var loggingConfiguration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", true, false)
            .AddEnvironmentVariables().Build();

        hostBuilder.ConfigureServices((hostContext, services) =>
        {
            services.AddLogging(loggingBuilder =>
            {
                var logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(loggingConfiguration)
                    .Enrich.FromLogContext()
                    .WriteTo.Console(new CompactJsonFormatter())
                    .CreateLogger();

                loggingBuilder.AddSerilog(logger, true);
            });
        });
    }

    public static void ConfigureServices(IHostBuilder hostBuilder)
    {
        hostBuilder.ConfigureServices((hostContext, services) =>
        {
            services.AddOptions();

            services.Configure<DatabaseSetting>(
                databaseSettings =>
                hostContext.Configuration.GetSection("Database").Bind(databaseSettings));

            services.Configure<NotificationServerSetting>(
                notificationServerSetting =>
                hostContext.Configuration.GetSection("NotificationServer").Bind(notificationServerSetting));

            services.AddSingleton<IServiceProvider, ServiceProvider>();

            services.AddSingleton<InMemoryNetworkState>();

            services.AddSingleton<PostgresWriter>();

            services.AddSingleton<IValidator, ElementNotFeededValidator>();

            services.AddHostedService<Worker>();

            services.AddSingleton<INotificationClient, NotificationServerClient>();

            services.AddSingleton<IProjection, RouteNetworkProjection>();

            services.AddSingleton<IEventStore>(
                e =>
                new PostgresEventStore(
                    serviceProvider: e.GetRequiredService<IServiceProvider>(),
                    connectionString: hostContext.Configuration.GetSection("EventStore").GetValue<string>("ConnectionString"),
                    databaseSchemaName: "events"
                )
            );
        });
    }
}
