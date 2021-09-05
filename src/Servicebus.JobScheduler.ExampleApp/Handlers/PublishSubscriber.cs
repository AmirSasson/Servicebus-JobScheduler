using Microsoft.Extensions.Logging;
using Servicebus.JobScheduler.Core.Contracts;
using Servicebus.JobScheduler.ExampleApp.Messages;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Servicebus.JobScheduler.ExampleApp.Handlers
{
    public class JobResultPublishingSubscriber : BaseSimulatorHandler<JobOutput>
    {
        private readonly ILogger _logger;
        private readonly string _runId;

        public JobResultPublishingSubscriber(ILogger<JobResultPublishingSubscriber> logger, string runId, int simulateFailurePercents) : base(simulateFailurePercents, TimeSpan.Zero, logger)
        {
            _logger = logger;
            _runId = runId;
            if (!File.Exists($"Joboutputs.{_runId}.csv"))
            {
                // write csv headers
                File.AppendAllLines($"Joboutputs.{_runId}.csv", new[] { "JobId,DateTime,WindowId,Id" });
            }
        }
        protected override Task<HandlerResponse<Topics>> handlePrivate(JobOutput msg)
        {
            _logger.LogWarning($"***********************************************");
            _logger.LogWarning($"NEW Job Result ARRIVED TO BE PUBLISHED!!! Published Job Result! [{msg.Id}] [{msg.RuleId}] [{msg.WindowId}] {msg.Name}");
            _logger.LogWarning($"***********************************************");
            File.AppendAllLines($"Joboutputs.{_runId}.csv", new[] { $"{msg.RuleId},{DateTime.UtcNow},{msg.WindowId},{msg.Id}" });
            return HandlerResponse<Topics>.FinalOkAsTask;
        }
    }
}
