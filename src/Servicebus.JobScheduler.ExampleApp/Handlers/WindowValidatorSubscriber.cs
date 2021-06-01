using Microsoft.Extensions.Logging;
using Servicebus.JobScheduler.Core.Contracts;
using Servicebus.JobScheduler.ExampleApp.Common;
using Servicebus.JobScheduler.ExampleApp.Messages;
using System;
using System.Threading.Tasks;

namespace Servicebus.JobScheduler.ExampleApp.Handlers
{
    public class WindowValidatorSubscriber : IMessageHandler<JobWindow>
    {
        private readonly IMessageBus<Topics, Subscriptions> _bus;
        private readonly IRepository<JobDefinition> _repo;
        private readonly string _runId; // just for test
        private readonly ILogger _logger;

        public WindowValidatorSubscriber(IMessageBus<Topics, Subscriptions> bus, ILogger<WindowValidatorSubscriber> logger, IRepository<JobDefinition> repo, string runId)
        {
            _bus = bus;
            _repo = repo;
            _runId = runId;
            _logger = logger;
        }

        public async Task<bool> Handle(JobWindow msg)
        {
            if (msg.RunId != _runId)
            {
                _logger.LogDebug("TEST ONLY We ignore other runs messages");
                return false;
            }

            if (msg.Interval <= TimeSpan.Zero)
            {
                _logger.LogCritical("Invalid Interval for rule!");
                return false;
            }
            if (msg.SkipValidation)
            {
                await _bus.PublishAsync(msg, Topics.JobWindowValid);
            }
            else
            {
                var latestRule = await _repo.GetById(msg.RuleId);
                if (latestRule == null || latestRule.Etag != msg.Etag)
                {
                    // rule was changed, dont re-schedule another loop will handle this..
                    // also catches enable/disable of rules
                    _logger.LogInformation($"rule {msg.RunId} was changed, dont re-schedule another loop will handle this.. {msg.Id}");
                }
                else
                {
                    _logger.LogInformation($"rule {msg.RunId} Valid LastRunWindowUpperBound:{msg.LastRunWindowUpperBound}");
                    await _bus.PublishAsync(msg, Topics.JobWindowValid);
                }
            }
            return true;
        }


    }
}
