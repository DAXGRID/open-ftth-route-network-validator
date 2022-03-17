using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;

namespace OpenFTTH.RouteNetwork.Validator
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            await Startup.CreateHostBuilder(args).Build().RunAsync();
        }
    }
}
