using Microsoft.Extensions.Logging;
using Servicebus.JobScheduler.Core.Contracts;
using Servicebus.JobScheduler.ExampleApp.Common;
using Servicebus.JobScheduler.ExampleApp.Messages;
using System;
using System.Threading.Tasks;

namespace Servicebus.JobScheduler.ExampleApp.Handlers
{
    public class RuleSupressorSubscriber : BaseSimulatorHandler<JobOutput>
    {
        private readonly IRepository<Job<JobCustomData>> _repo;
        private readonly ILogger _logger;

        public RuleSupressorSubscriber(IRepository<Job<JobCustomData>> repo, ILogger<RuleSupressorSubscriber> logger, int simulateFailurePercents) : base(simulateFailurePercents, TimeSpan.Zero, logger)
        {
            _repo = repo;
            _logger = logger;
        }
        protected override async Task<HandlerResponse> handlePrivate(JobOutput msg)
        {
            if (msg.JobSource.Payload.BehaviorMode == JobCustomData.JobBehaviorMode.DisabledAfterFirstJobOutput)
            {
                var rule = await _repo.GetById(msg.Id);
                if (rule.Payload.BehaviorMode == JobCustomData.JobBehaviorMode.DisabledAfterFirstJobOutput)
                {
                    _logger.LogInformation($"Supressing Rule {msg.Id}");
                    rule.Status = JobStatus.Disabled;
                    await _repo.Upsert(rule);
                }
            }
            return HandlerResponse.FinalOk;
        }
    }
}
