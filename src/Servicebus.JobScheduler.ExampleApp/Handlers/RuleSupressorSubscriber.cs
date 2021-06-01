using Microsoft.Extensions.Logging;
using Servicebus.JobScheduler.Core.Contracts;
using Servicebus.JobScheduler.ExampleApp.Common;
using Servicebus.JobScheduler.ExampleApp.Messages;
using System.Threading.Tasks;

namespace Servicebus.JobScheduler.ExampleApp.Handlers
{
    public class RuleSupressorSubscriber : IMessageHandler<JobOutput>
    {
        private readonly IRepository<JobDefinition> _repo;
        private readonly ILogger _logger;

        public RuleSupressorSubscriber(IRepository<JobDefinition> repo, ILogger<RuleSupressorSubscriber> logger)
        {
            _repo = repo;
            _logger = logger;
        }
        public async Task<bool> Handle(JobOutput msg)
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
