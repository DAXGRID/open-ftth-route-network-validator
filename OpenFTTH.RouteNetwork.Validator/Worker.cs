using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DAX.EventProcessing.Dispatcher;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenFTTH.Events.RouteNetwork;
using OpenFTTH.RouteNetwork.Validator.Config;
using OpenFTTH.RouteNetwork.Validator.State;
using Topos.Config;
using Topos.InMem;

namespace OpenFTTH.RouteNetwork.Validator
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IOptions<KafkaSetting> _kafkaSetting;
        private readonly IToposTypedEventMediator<RouteNetworkEvent> _eventMediator;
        private readonly InMemoryNetworkState _inMemoryNetworkState;
        private IDisposable _kafkaConsumer;

        private InMemPositionsStorage _positionsStorage = new InMemPositionsStorage();

        public Worker(ILogger<Worker> logger, IOptions<KafkaSetting> kafkaSetting, IToposTypedEventMediator<RouteNetworkEvent> eventMediator, InMemoryNetworkState inMemoryNetworkState)
        {
            _logger = logger;
            _kafkaSetting = kafkaSetting;
            _eventMediator = eventMediator;
            _inMemoryNetworkState = inMemoryNetworkState;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting route network event consumer worker at: {time}", DateTimeOffset.Now);

            try
            {
                _kafkaConsumer = _eventMediator.Config("validator_route_network_event_consumer_" + Guid.NewGuid(), c => c.UseKafka(_kafkaSetting.Value.Server))
                             .Logging(l => l.UseSerilog())
                             .Positions(p => p.StoreInMemory(_positionsStorage))
                             .Topics(t => t.Subscribe(_kafkaSetting.Value.RouteNetworkEventTopic))
                             .Start();

                // Wait for load mode to create an initial version/state
                _logger.LogInformation("Starting load mode...");
                bool loadFinish = false;
                while (!stoppingToken.IsCancellationRequested && !loadFinish)
                {
                    _logger.LogDebug("Waiting for load mode to finish creating initial state...");

                    _logger.LogInformation($"{_inMemoryNetworkState.NumberOfObjectsLoaded} objects loaded.");

                    DateTime waitStartTimestamp = DateTime.UtcNow;

                    await Task.Delay(5000, stoppingToken);

                    TimeSpan timespan = waitStartTimestamp - _inMemoryNetworkState.LastEventRecievedTimestamp;

                    if (timespan.TotalSeconds > 10)
                    {
                        loadFinish = true;
                    }
                }

                _inMemoryNetworkState.FinishLoadMode();
                _logger.LogInformation("Loading of initial state finished.");

                // We are now ready to serve the public if the loaded objects are bigger than 0
                if (_inMemoryNetworkState.NumberOfObjectsLoaded > 0)
                    File.Create("/tmp/healthy");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }

            await Task.CompletedTask;
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Stopping background worker");
            _kafkaConsumer.Dispose();

            await Task.CompletedTask;
        }
    }
}
