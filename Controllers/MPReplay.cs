using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using STRATUS.CAD.Models.Enums;
using STRATUS.CAD.Models.PipelineModels;
using STRATUS.CAD.Models.TelemetryModels;
using STRATUS.CAD.Repos.SQL;
using STRATUS.CAD.Services.PipelineServices;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace Replay.Controllers
{
    internal class MPReplay
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

        static public async Task<int> Main(IConfigurationRoot config, ServiceProvider services, string[] args)
        {
            _config = config;
            _services = services;

            if (args?.Length != 1) return Usage(1);
            if (!int.TryParse(args[0], out var jobId)) return Usage(2);

            Console.WriteLine("Working on JobId: " + args[0]);
            try
            {
                // var telemetryRepo = _services.GetRequiredService<TelemetryRepo>();
                var orchestrationService = _services.GetRequiredService<OrchestrationService>();
                var sql = _services.GetRequiredService<SQLConnectionString>();
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

        /*
                var replayRequest1 = new ReplayRequest
                {
                    ActivityDefinitionId = 33, // JsonToDocumentDB
                    ActivityEventId = 10896402, // e.g., 10554188, gotten FROM [dbo].[Telemetry] where JobId=<abc> and (Message like '%<fname>%' and ActivityDefintionId=27) 
                    DbCreateDateTime = DateTime.UtcNow,
                    Id = 0,
                    JobId = 34675,
                    ReplayRequestType = ReplayRequestType.IsolatedEvent,
                    RequestDateTime = DateTime.UtcNow,
                    RequestedByUserId = Environment.UserName
                };
                var replay2 = orchestrationService.StartReplayAsync(replayRequest1);
                Task.WaitAll(replay2);
                Console.WriteLine("Event replayed. Now wait for PreloadCheckpoint to catch up.");
                return 1;
       */

        public string ReplayMetaDataForBim360Polling()
        {
            string mock = @"
            {'JobId':34675,'CompanyGuid':'2ddde471-147c-4811-8b0e-18537ee31ac0','ProjectGuid':'187074af-5cfa-4636-a634-9e57bc098668','ModelGuid':'9d420868-6549-4f02-ae46-0d041c2c89c8','ModelId':2516,'GtpModelVersionId':32932,'JobGtpModelVersionId':32932,'FileName':'220985-HP-FAB-v20.rvt','Bim360LinkId':'urn:adsk.wipprod:fs.file:vf.KRx9fVqDQgyVdDNaFEYOPg?version=61','Bim360ItemLink':'https://developer.api.autodesk.com/oss/v2/buckets/wip.dm.prod/objects/2c3ab368-678f-46a8-bcbf-183b8c3e8734.rvt?scopes=b360project.f698ff9a-caaf-4091-b170-695258b94e8b,O2tenant.5629303','StratusUserId':'c81703e9-7b47-4d45-ad2f-0815f4badc39','Bim360UserId':'RUVPKZPQ683B','ModelTranslationStatusId':1}
            ";
            return mock;
        }

        public void ReplayEvent(int activityDefiniit = 33)
        {

            var replayRequest = new ReplayRequest
            {
                ActivityDefinitionId = 33, // JsonToDocumentDB
                ActivityEventId = 10896402, // e.g., 10554188, gotten FROM [dbo].[Telemetry] where JobId=<abc> and (Message like '%<fname>%' and ActivityDefintionId=27) 
                DbCreateDateTime = DateTime.UtcNow,
                Id = 0,
                JobId = 34675,
                ReplayRequestType = ReplayRequestType.IsolatedEvent,
                RequestDateTime = DateTime.UtcNow,
                RequestedByUserId = Environment.UserName
            };

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
            foreach (var part in files)
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
