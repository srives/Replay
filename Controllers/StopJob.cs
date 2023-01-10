using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using STRATUS.CAD.Models.Enums;
using STRATUS.CAD.Models.PipelineModels;
using STRATUS.CAD.Services.AzureServices;
using STRATUS.CAD.Services.PipelineServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Replay.Controllers
{
    internal class StopJob
    {
        public static IConfigurationRoot _config = null;
        public static ServiceProvider _services = null;

        public static int Usage(int errorCode)
        {
            Console.WriteLine("Usage: StopJob jobId");
            return errorCode;
        }

        static public async Task<int> Main(IConfigurationRoot config, ServiceProvider services, string[] args)
        {
            if (args.Length != 2)
            {
                return Usage(1);
            }
            if (!int.TryParse(args[1], out var jobId))
            {
                Console.WriteLine("Invalid JobId number.");
                return Usage(2);
            }
            _config = config;
            _services = services;
            var jobOperationService = _services.GetRequiredService<JobOperationService>();
            var jobOperation = new JobOperation
            {
                Id = 0,
                IsFailure = false,
                JobId = jobId,
                OperationDateTime = DateTime.Now,
                OperationType = JobOperationType.Stopped,
                RequestedByUserId = "Steve Rives"
            };

            string oAuthId = string.Empty;
            await jobOperationService.CreateAsync(jobOperation, oAuthId);
            return Usage(1);
        }

        public void Stop(int JobId)
        {
        }
    }
}
