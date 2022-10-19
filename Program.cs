using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver.Core.Configuration;
using STRATUS.CAD.Models.Enums;
using STRATUS.CAD.Models.PipelineModels;
using STRATUS.CAD.Models.TelemetryModels;
using STRATUS.CAD.Repos;
using STRATUS.CAD.Repos.SQL;
using STRATUS.CAD.Services;
using STRATUS.CAD.Services.AzureServices;
using STRATUS.CAD.Services.PipelineServices;
using STRATUS.CAD.StartupResources;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Replay
{

    internal class Program
    {
        public static IConfigurationRoot _config = null;
        public static ServiceProvider _services = null;

        public static int Usage(int errorCode)
        {
            Console.WriteLine("Usage: Replay <Job Id>");
            Console.WriteLine("       GTP Model Pipeline Replay Tool");
            Console.WriteLine("       This program will replay the JsonToDocumentDB jobs that are causing a stall in PreloadCheckpoin.");
            Console.WriteLine("       Defaults to production. To change this to QA from the command line, run: Set Environment=QA");
            Console.WriteLine("       Example: ");
            Console.WriteLine("                Replay 33110");
            return errorCode;
        }

        static async Task<int> Main(string[] args)
        {
            if (args?.Length != 1) return Usage(1); 
            if (!int.TryParse(args[0], out var jobId)) return Usage(2);

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Environment")))
            {
                Environment.SetEnvironmentVariable("Environment", "Production");
            }
            Console.WriteLine("Environment: " + Environment.GetEnvironmentVariable("Environment"));
            Console.WriteLine("Working on JobId: " + args[0]);
            try
            {
                CreateConfigAndServices();
                var orchestrationService = _services.GetRequiredService<OrchestrationService>();
                // var telemetryRepo = _services.GetRequiredService<TelemetryRepo>(); // I did use the TelemetryRepo, but I couldn't check in my code with it, so using my own Service (SQL
                var sql = _services.GetRequiredService<SQLConnectionString>();
                var block = _services.GetRequiredService<BlockBlobService>();

                /*
                FileStream testStream = new FileStream(
                // Upload a bunch of files, and see if (base 64, UUEncoded files breaks? Code that writes blocks can't handle)
                await block.UploadAsync(Guid.Parse("02ff41d0-c462-4fd3-b4bb-d761172c3148"),
                "https://modeldataqa.blob.core.windows.net/02ff41d0-c462-4fd3-b4bb-d761172c3148/fabconfig/Fabrication2022-TweetGarot/db_items_images_items.zip", testStream);
                */

                var events = await GetUnfinishedEventsAsync(sql.ConnectionString, null, jobId);
                if (events?.Count() > 0)
                {
                    foreach (var item in events)
                    {
                        var replayRequest = new ReplayRequest
                        {
                            ActivityDefinitionId = item.ActivityDefinitionId, // JsonToDocumentDB
                            ActivityEventId = item.ActivityEventId, // e.g., 10554188, gotten FROM [dbo].[Telemetry] where JobId=<abc> and (Message like '%<fname>%' and ActivityDefintionId=27) 
                            DbCreateDateTime = DateTime.UtcNow,
                            Id = 0,
                            JobId = jobId,
                            ReplayRequestType = ReplayRequestType.IsolatedEvent,
                            RequestDateTime = DateTime.UtcNow,
                            RequestedByUserId = Environment.UserName
                        };

                        var replay = orchestrationService.StartReplayAsync(replayRequest);
                        Task.WaitAll(replay);
                        Console.WriteLine("Event replayed. Now wait for PreloadCheckpoint to catch up.");
                    }
                }
                else
                {
                    Console.WriteLine("Did not find any JsonToDocumentDB events to replay.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return 3;
            }
            return 0;
        }

        public static void CreateConfigAndServices()
        {
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
        
        public static async Task<Telemetry> GetNewestMessageLikeAsync(string connectionString, int jobId, string likeThis, int activityDefinitionId = -1)
        {
            Telemetry answer = null;
            using (var conn = new SqlConnection(connectionString))
            {
                var query = $@"SELECT top(1)
                                      [Id]
                                    , [ActivityEventId]
                                    , [ActivityDefinitionId]
                                    , [Message]
                                    , [TelemetryTypeId]
                                    , [Exception]
                                    , [CreateDateTime]
                                    , [DbCreateDateTime]
                                    , [SourceSystemTypeId]
                            FROM    [dbo].[Telemetry] WITH (NOLOCK)
                            WHERE   [JobId] = {jobId} AND [Message] LIKE '{likeThis}'";

                if (activityDefinitionId != -1)
                {
                    query += $" AND [ActivityDefinitionId] = {activityDefinitionId}";
                }
                query += " ORDER BY [CreateDateTime] desc";

                await conn.OpenAsync();
                var result = await conn.QueryAsync<Telemetry>(query);
                answer = result?.ToList().FirstOrDefault();
            }
            return answer;
        }
        
        public static async Task<List<Telemetry>> GetUnfinishedEventsAsync(string connectionString, TelemetryRepo telemetryRepo, int jobId)
        {
            var unfinishedEvents = new List<Telemetry>();
            var what = "PreloadCheckpoint";

            // PreloadCheckpoint logs the files that it is waiting for
            var telemetry = await GetNewestMessageLikeAsync(connectionString, jobId, $"%{what} waiting on files:%");

            if (telemetry == null)
            {
                what = "CommitModelService";
                telemetry = await GetNewestMessageLikeAsync(connectionString, jobId, $"%{what} waiting on files:%");
            }

            if (telemetry == null)
            {
                return unfinishedEvents;
            }

            // telemetry.Message is format: "PreloadCheckpoint waiting on files: x_webidtoelementid-batch-29fa9cfc-b007-e0df-5fe7-fcc06a98f17b-0001.jsonz, x_webidtoelementid-batch-7e84c8ca-7367-73a7-675d-29d9d025c0bf-0001.jsonz."
            var files = telemetry.Message.Split(new char[] { ',', ':' });
            foreach(var part in files)
            {
                if (part.Contains(what)) continue;
                var file = part.Trim().TrimEnd('.');
                Console.Write(file);
                telemetry = await GetNewestMessageLikeAsync(connectionString, jobId, $"%{file}%", 27); // 27 is the JsonToDocumentDB Definition ID we care about
                if (telemetry != null)
                {
                    Console.WriteLine($": {what} is Waiting on File: {file} from JsonToDocumentDb with EventId = {telemetry.ActivityEventId}.");
                    unfinishedEvents.Add(telemetry);
                }
                else
                {
                    Console.WriteLine($": There is no notice of JsonToDocumentDb (ActivityDefinitionId = 27) having procssed this file, therefore it cannot be replayed.");
                }
            }
            return unfinishedEvents;
        }
    }
}