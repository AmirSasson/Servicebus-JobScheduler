using Microsoft.Extensions.Logging;
using Servicebus.JobScheduler.Core.Contracts;
using Servicebus.JobScheduler.Core.Utils;
using Servicebus.JobScheduler.ExampleApp.Messages;
using System;
using System.Threading.Tasks;

namespace Servicebus.JobScheduler.ExampleApp.Handlers
{
    public class WindowExecutionSubscriber : BaseSimulatorHandler<JobWindow>
    {
        private readonly IMessageBus<Topics, Subscriptions> _bus;
        private readonly ILogger _logger;
        private readonly int _simulateFailurePercents;
        static int counterDummy;

        public WindowExecutionSubscriber(IMessageBus<Topics, Subscriptions> bus, ILogger<WindowExecutionSubscriber> logger, int simulateFailurePercents, TimeSpan simulateExecutionTime)
        : base(simulateFailurePercents, simulateExecutionTime, logger)
        {
            _bus = bus;
            _logger = logger;
            _simulateFailurePercents = simulateFailurePercents;
        }
        protected override async Task<bool> handlePrivate(JobWindow msg)
        {
            var result = await runRuleCondition(msg);

            if (result.conditionMet)
            {
                await _bus.PublishAsync(new JobOutput { Id = Guid.NewGuid().ToString(), Name = "", WindowId = msg.Id, RuleId = msg.RuleId, RunId = msg.RunId, Rule = msg }, Topics.JobWindowConditionMet);
            }
            else
            {
                var execContext = msg.ToJson().FromJson<JobWindowExecutionContext>();
                execContext.WindowExecutionTime = DateTime.UtcNow;
                await _bus.PublishAsync(execContext, Topics.JobWindowConditionNotMet);
            }

            return true;
        }

        private Task<(bool conditionMet, object result)> runRuleCondition(JobWindow msg)
        {
            counterDummy++;
            _logger.LogWarning($"Simulating window call {msg.FromTime:hh:mm:ss}-{msg.ToTime:hh:mm:ss} call to long unstable dependency for JobId {msg.RuleId} call: #{counterDummy}");
            //   if (counterDummy % 2 == 0)
            //   {
            return Task.FromResult<(bool conditionMet, object result)>((conditionMet: true, result: null));
            //   }
            //   return (conditionMet: false, result: null);
        }
    }
}
