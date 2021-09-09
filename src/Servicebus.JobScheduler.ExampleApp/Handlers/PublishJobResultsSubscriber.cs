using Microsoft.Extensions.Logging;
using Servicebus.JobScheduler.Core.Contracts;
using Servicebus.JobScheduler.ExampleApp.Common;
using Servicebus.JobScheduler.ExampleApp.Messages;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Servicebus.JobScheduler.ExampleApp.Handlers
{
    public class PublishJobResultsSubscriber : BaseSimulatorHandler<JobOutput>
    {
        private readonly ILogger _logger;
        private readonly string _runId;

        public PublishJobResultsSubscriber(ILogger<PublishJobResultsSubscriber> logger, string runId, int simulateFailurePercents) : base(simulateFailurePercents, TimeSpan.Zero, logger)
        {
            _logger = logger;
            _runId = runId;
            if (!File.Exists($"Joboutputs.{_runId}.csv"))
            {
                // write csv headers
                File.AppendAllLines($"Joboutputs.{_runId}.csv", new[] { "OutputId,RuleId,TimeGenerated,WindowUpperBoundEpoch,WindowId" });
            }
        }
        protected override Task<HandlerResponse> handlePrivate(JobOutput msg)
        {
            _logger.LogWarning($"***********************************************");
            _logger.LogWarning($"NEW Job Result ARRIVED TO BE PUBLISHED!!! Published Job Result! [{msg.Id}] [{msg.JobSource.RuleId}] [{msg.WindowId}] {msg.Name}");
            _logger.LogWarning($"***********************************************");
            File.AppendAllLines($"Joboutputs.{_runId}.csv", new[] { $"{msg.Id},{msg.JobSource.RuleId},{DateTime.UtcNow},{new DateTimeOffset(msg.WindowToTime).ToUnixTimeSeconds()},{msg.WindowId}" });
            return HandlerResponse.FinalOkAsTask;
        }
    }
}
