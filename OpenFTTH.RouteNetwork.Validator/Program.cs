using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace OpenFTTH.RouteNetwork.Validator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                Startup.CreateHostBuilder(args).Build().Run();
            }
            catch (Exception ex)
            {

            }
        }
    }
}
