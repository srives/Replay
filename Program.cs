using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Replay.Controllers;
using STRATUS.CAD.Repos;
using STRATUS.CAD.Services;
using STRATUS.CAD.StartupResources;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Replay
{
    internal class Program
    {
        public static IConfigurationRoot _config = null;
        public static ServiceProvider _services = null;

        static async Task<int> Main(string[] args)
        {
            if (args.Length == 0) return Usage(1);

            CreateConfigAndServices("QA");
            if (args.Length == 1)
                await MPReplay.Main(_config, _services, args);
            else if (args[0].ToLower().Contains("user"))
                await User.Main(_config, _services, args);
            else if (args[0].ToLower().Contains("ftp"))
                await FTP.Main(_config, _services, args);
            return 1;
        }

        public static void CreateConfigAndServices(string environment = "Production")
        {
            Environment.SetEnvironmentVariable("Environment", environment);            
            Console.WriteLine("Environment: " + Environment.GetEnvironmentVariable("Environment"));

            _config = new ConfigurationBuilder()
                            .SetBasePath(Path.Combine(AppContext.BaseDirectory))
                            .AddGTPAzureKeyVaults(Path.Combine(AppContext.BaseDirectory))
                            .Build();

            IServiceCollection services = new ServiceCollection();
            services.AddTransient(typeof(SQLConnectionString));
            services.AddSingleton(_config);
            services.AddSingleton(typeof(OptionsManager<>));
            services.AddLogging();
            services.ConfigureRepos(_config);
            services.ConfigureServices(_config);

            _services = services.BuildServiceProvider();
        }

        public static int Usage(int errorcode)
        {
            MPReplay.Usage(errorcode);
            FTP.Usage(errorcode);
            User.Usage(errorcode);
            return errorcode;
        }
    }
}