using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Replay.Controllers;
using STRATUS.CAD.Repos;
using STRATUS.CAD.Services;
using STRATUS.CAD.StartupResources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Replay
{
    static class Args
    {
        static public bool UseLocalSecerts { get; set; } = true;
        static public string Command { get; set; } = string.Empty;

        static private List<string> _args = new List<string>();
        static public string[] args {get { return _args.ToArray(); } }
        static public void Add(string arg)
        {
            _args.Add(arg);
        }
    }

    internal class Program
    {
        public static IConfigurationRoot _config = null;
        public static ServiceProvider _services = null;        

        static async Task<int> Main(string[] args)
        {            
            if (args.Length == 0) return Usage(1);
            ParseArgs(args);

            CreateConfigAndServices(Args.UseLocalSecerts, "Production");
            if (Args.args.Length == 1)
                await MPReplay.Main(_config, _services, args);
            else if (Args.Command.ToLower().Contains("user"))
                await User.Main(_config, _services, args);
            else if (Args.Command.ToLower().Contains("ftp"))
                await FTP.Main(_config, _services, args);
            else if (Args.Command.ToLower().Contains("folder"))
                await Bim360Folder.Main(_config, _services, args);
            else if (Args.Command.ToLower().Contains("mongo"))
                await Mongo.Main(_config, _services, args);
            else if (Args.Command.ToLower().Contains("stop"))
                await StopJob.Main(_config, _services, args);
            else
                return Usage(1);
            return 1;
        }

        static void ParseArgs(string[] args)
        {
            foreach (var arg in args)
            {
                if (arg.ToLower().StartsWith("-az") || arg.ToLower().StartsWith("--az") || arg.ToLower().StartsWith("az"))
                {
                    Args.UseLocalSecerts = false;
                }
                else
                {
                    if (string.IsNullOrEmpty(Args.Command))
                    {
                        Args.Command = arg;
                    }
                    Args.Add(arg);
                }
            }
            if (Args.UseLocalSecerts == true)
            {
                Console.WriteLine("Using local secrets. Consider refreshing them.");
            }
        }

        public static void CreateConfigAndServices(bool useLocalSecrets, string environment = "Production")
        {
            Environment.SetEnvironmentVariable("Environment", environment);            
            Console.WriteLine("Environment: " + Environment.GetEnvironmentVariable("Environment"));

            var secrets = useLocalSecrets ? Path.Combine(AppContext.BaseDirectory) : "\\";

            _config = new ConfigurationBuilder()
                            .SetBasePath(Path.Combine(AppContext.BaseDirectory))
                            .AddGTPAzureKeyVaults(secrets)
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
            Bim360Folder.Usage(errorcode);
            StopJob.Usage(errorcode);
            Mongo.Usage(errorcode);
            return errorcode;
        }
    }
}