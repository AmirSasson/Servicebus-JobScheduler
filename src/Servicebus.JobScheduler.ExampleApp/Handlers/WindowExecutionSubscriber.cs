using Microsoft.Extensions.Logging;
using Servicebus.JobScheduler.Core.Contracts;
using Servicebus.JobScheduler.Core.Utils;
using Servicebus.JobScheduler.ExampleApp.Messages;
using System;
using System.Threading.Tasks;

namespace Servicebus.JobScheduler.ExampleApp.Handlers
{
    public class WindowExecutionSubscriber : IMessageHandler<JobWindow>
    {
        private readonly IMessageBus<Topics, Subscriptions> _bus;
        private readonly ILogger _logger;
        private readonly int _simulateFailurePercents;
        static int counterDummy;

        public WindowExecutionSubscriber(IMessageBus<Topics, Subscriptions> bus, ILogger<WindowExecutionSubscriber> logger, int simulateFailurePercents)
        {
            _bus = bus;
            _logger = logger;
            _simulateFailurePercents = simulateFailurePercents;
        }
        public async Task<bool> Handle(JobWindow msg)
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

        private async Task<(bool conditionMet, object result)> runRuleCondition(JobWindow msg)
        {
            var rand = new Random((int)DateTime.Now.Ticks);

            bool shouldSimulateError()
            {
                //return counterDummy < 35 && rand.Next(0, 100) <= _simulateFailurePercents;
                return rand.Next(0, 100) <= _simulateFailurePercents;
            }
            counterDummy++;
            _logger.LogWarning($"Simulating window call {msg.FromTime:hh:mm:ss}-{msg.ToTime:hh:mm:ss} call to log analytics for ruleId {msg.Id} call: #{counterDummy}");
            await Task.Delay(TimeSpan.FromSeconds(1));
            if (shouldSimulateError())
            {
                throw new ApplicationException("Error simulation..");
            }
            //   if (counterDummy % 2 == 0)
            //   {
            return (conditionMet: true, result: null);
            //   }
            //   return (conditionMet: false, result: null);
        }
    }
}
