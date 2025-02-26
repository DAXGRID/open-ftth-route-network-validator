using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenFTTH.EventSourcing;
using System.Threading.Tasks;

namespace OpenFTTH.RouteNetwork.Validator;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var host = Startup.CreateHostBuilder(args).Build();

        // We scan projections here, so we do not have to worry about it
        // in other places.
        host.Services.GetService<IEventStore>()!.ScanForProjections();

        await host.RunAsync().ConfigureAwait(false);
    }
}
