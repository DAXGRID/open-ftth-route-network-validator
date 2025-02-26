using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.Validator.State;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OpenFTTH.RouteNetwork.Validator;

internal sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly InMemoryNetworkState _inMemoryNetworkState;
    private readonly IEventStore _eventStore;

    public Worker(
        ILogger<Worker> logger,
        InMemoryNetworkState inMemoryNetworkState,
        IEventStore eventStore)
    {
        _logger = logger;
        _inMemoryNetworkState = inMemoryNetworkState;
        _eventStore = eventStore;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        const int LISTEN_EVENTS_INTERVAL = 250;

        try
        {
            _logger.LogInformation("Starting reading all events.");
            await _eventStore.DehydrateProjectionsAsync(stoppingToken).ConfigureAwait(false);

            _logger.LogInformation("Loading of initial state finished.");
            _inMemoryNetworkState.FinishLoadMode();

            // Cleanup generation 2 objects.
            GC.Collect(2, GCCollectionMode.Aggressive);

            _ = File.Create("/tmp/healthy");
            _logger.LogInformation("Healthy file has been written.");

            _logger.LogInformation("Starting listening for new events.");
            await ListenEvents(LISTEN_EVENTS_INTERVAL, stoppingToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError("{Exception}", ex);
            throw;
        }
    }

    private async Task ListenEvents(int delay, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            await _eventStore.CatchUpAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    public override Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Stopping background worker");
        return Task.CompletedTask;
    }
}
