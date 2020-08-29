using DAX.EventProcessing.Dispatcher;
using DAX.EventProcessing.Dispatcher.Topos;
using DAX.ObjectVersioning.Core;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using OpenFTTH.Events.RouteNetwork;
using OpenFTTH.RouteNetwork.Validator.Config;
using OpenFTTH.RouteNetwork.Validator.Database.Impl;
using OpenFTTH.RouteNetwork.Validator.Handlers;
using OpenFTTH.RouteNetwork.Validator.Producer;
using OpenFTTH.RouteNetwork.Validator.State;
using OpenFTTH.RouteNetwork.Validator.Validators;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenFTTH.RouteNetwork.Validator
{
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
                   return settings;
               });
        }

        private static void ConfigureLogging(IHostBuilder hostBuilder)
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", true, false)
                .AddEnvironmentVariables().Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            hostBuilder.ConfigureServices((hostContext, services) =>
            {
                services.AddLogging(loggingBuilder =>
                {
                    var logger = new LoggerConfiguration()
                        .ReadFrom.Configuration(configuration)
                        .CreateLogger();

                    loggingBuilder.AddSerilog(dispose: true);
                });
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

                services.AddSingleton<IServiceProvider, ServiceProvider>();

                // MediatR
                services.AddMediatR(typeof(Startup));

                // Kafka producer and consumer stuff
                services.AddSingleton<IToposTypedEventMediator<RouteNetworkEvent>, ToposTypedEventMediator<RouteNetworkEvent>>();
                services.AddSingleton<IProducer, Producer.Kafka.Producer>();

                // In memory state manager
                services.AddSingleton<InMemoryNetworkState>();

                // Event handler
                services.AddSingleton<RouteNetworkEventHandler>();

                // Database stuff
                services.AddSingleton<PostgresWriter>();

                // Validators
                services.AddSingleton<IValidator, ElementNotFeededValidator>();

                // The worker
                services.AddHostedService<Worker>();
            });
        }
    }
}
