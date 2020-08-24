using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DAX.EventProcessing.Dispatcher;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenFTTH.Events.RouteNetwork;
using OpenFTTH.RouteNetwork.Validator.Config;
using Topos.Config;

namespace OpenFTTH.RouteNetwork.Validator
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IOptions<KafkaSetting> _kafkaSetting;
        private readonly IToposTypedEventMediator<RouteNetworkEvent> _eventMediator;

        public Worker(ILogger<Worker> logger, IOptions<KafkaSetting> kafkaSetting, IToposTypedEventMediator<RouteNetworkEvent> eventMediator)
        {
            _logger = logger;
            _kafkaSetting = kafkaSetting;
            _eventMediator = eventMediator;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting route network event consumer worker at: {time}", DateTimeOffset.Now);

            _eventMediator.Config("validator_route_network_event_consumer", c => c.UseKafka(_kafkaSetting.Value.Server))
                          .Logging(l => l.UseSerilog())
                          .Positions(p => p.StoreInFileSystem(_kafkaSetting.Value.PositionFilePath))
                          .Topics(t => t.Subscribe(_kafkaSetting.Value.RouteNetworkEventTopic))
                          .Start();
        }
    }
}
