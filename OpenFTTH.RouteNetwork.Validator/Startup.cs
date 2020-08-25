using DAX.EventProcessing.Dispatcher;
using DAX.EventProcessing.Dispatcher.Topos;
using DAX.ObjectVersioning.Core;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenFTTH.Events.RouteNetwork;
using OpenFTTH.RouteNetwork.Validator.Config;
using OpenFTTH.RouteNetwork.Validator.Database.Impl;
using OpenFTTH.RouteNetwork.Validator.Handlers;
using OpenFTTH.RouteNetwork.Validator.State;
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
             .MinimumLevel.Verbose()
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

                // MediatR
                services.AddMediatR(typeof(Startup));
                
                // Route network event consumer/dispatcher
                services.AddSingleton<IToposTypedEventMediator<RouteNetworkEvent>, ToposTypedEventMediator<RouteNetworkEvent>>();
                services.AddSingleton<InMemoryNetworkState>();

                // Event handler
                services.AddSingleton<RouteNetworkEventHandler>();

                services.AddSingleton<PostgressWriter>();

                // The worker
                services.AddHostedService<Worker>();
            });
        }
    }
}
