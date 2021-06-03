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
        private readonly IRepository<JobDefinition> _repo;
        private readonly ILogger _logger;

        public RuleSupressorSubscriber(IRepository<JobDefinition> repo, ILogger<RuleSupressorSubscriber> logger, int simulateFailurePercents) : base(simulateFailurePercents, TimeSpan.Zero, logger)
        {
            _repo = repo;
            _logger = logger;
        }
        protected override async Task<bool> handlePrivate(JobOutput msg)
        {
            if (msg.Rule.BehaviorMode == JobDefinition.JobBehaviorMode.DisabledAfterFirstJobOutput)
            {
                var rule = await _repo.GetById(msg.RuleId);
                if (rule.BehaviorMode == JobDefinition.JobBehaviorMode.DisabledAfterFirstJobOutput)
                {
                    _logger.LogInformation($"Supressing Rule {msg.RuleId}");
                    rule.Status = JobDefinition.JobStatus.Disabled;
                    await _repo.Upsert(rule);
                }
            }
            return true;
        }
    }
}
