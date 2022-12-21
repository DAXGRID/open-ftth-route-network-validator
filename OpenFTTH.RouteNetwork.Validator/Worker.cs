using DAX.EventProcessing.Dispatcher;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenFTTH.Events.RouteNetwork;
using OpenFTTH.RouteNetwork.Validator.Config;
using OpenFTTH.RouteNetwork.Validator.Handlers;
using OpenFTTH.RouteNetwork.Validator.State;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Topos.Config;
using Topos.InMem;

namespace OpenFTTH.RouteNetwork.Validator
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IOptions<KafkaSetting> _kafkaSetting;
        private readonly IToposTypedEventObservable<RouteNetworkEditOperationOccuredEvent> _routeNetworkEventObserver;
        private readonly InMemoryNetworkState _inMemoryNetworkState;
        private readonly RouteNetworkEventHandler _routeNetworkEventHandler;
        private IDisposable _kafkaConsumer;

        private InMemPositionsStorage _positionsStorage = new InMemPositionsStorage();

        public Worker(
            ILogger<Worker> logger,
            IOptions<KafkaSetting> kafkaSetting,
            IToposTypedEventObservable<RouteNetworkEditOperationOccuredEvent> routeNetworkObserver,
            InMemoryNetworkState inMemoryNetworkState,
            RouteNetworkEventHandler routeNetworkEventHandler)
        {
            _logger = logger;
            _kafkaSetting = kafkaSetting;
            _routeNetworkEventObserver = routeNetworkObserver;
            _inMemoryNetworkState = inMemoryNetworkState;
            _routeNetworkEventHandler = routeNetworkEventHandler;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting route network event consumer worker at: {time}", DateTimeOffset.Now);

            if (_kafkaSetting.Value.RouteNetworkEventTopic == null)
                throw new ArgumentException("routeNetworkEventTopic must be specified!");

            try
            {
                var toposConfig = _routeNetworkEventObserver.Config("validator_route_network_event_consumer_" + Guid.NewGuid(), c =>
                {
                    var kafkaConfig = c.UseKafka(_kafkaSetting.Value.Server);

                    if (_kafkaSetting.Value.CertificateFilename != null)
                    {
                        kafkaConfig.WithCertificate(_kafkaSetting.Value.CertificateFilename);
                    }
                })
                    .Logging(l => l.UseSerilog())
                    .Positions(p => p.StoreInMemory(_positionsStorage))
                    .Topics(t => t.Subscribe(_kafkaSetting.Value.RouteNetworkEventTopic));

                _routeNetworkEventObserver.OnEvent.Subscribe(_routeNetworkEventHandler);
                _kafkaConsumer = toposConfig.Start();

                // Wait for load mode to create an initial version/state
                _logger.LogInformation("Starting load mode...");

                bool loadFinish = false;
                while (!stoppingToken.IsCancellationRequested && !loadFinish)
                {
                    _logger.LogDebug("Waiting for load mode to finish creating initial state...");

                    _logger.LogInformation($"{_inMemoryNetworkState.NumberOfObjectsLoaded} objects loaded.");

                    var waitStartTimestamp = DateTime.UtcNow;

                    await Task.Delay(5000, stoppingToken);

                    var timeSpan = waitStartTimestamp - _inMemoryNetworkState.LastEventRecievedTimestamp;

                    if (timeSpan.TotalSeconds > 10)
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
        }

        public override Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Stopping background worker");
            _kafkaConsumer.Dispose();
            return Task.CompletedTask;
        }
    }
}
