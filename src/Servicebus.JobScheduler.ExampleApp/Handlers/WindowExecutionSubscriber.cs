using Microsoft.Extensions.Logging;
using Servicebus.JobScheduler.Core.Contracts;
using Servicebus.JobScheduler.Core.Utils;
using Servicebus.JobScheduler.ExampleApp.Common;
using Servicebus.JobScheduler.ExampleApp.Messages;
using System;
using System.Threading.Tasks;

namespace Servicebus.JobScheduler.ExampleApp.Handlers
{
    public class WindowExecutionSubscriber : BaseSimulatorHandler<JobWindow<JobCustomData>>
    {
        private readonly ILogger _logger;
        private readonly int _simulateFailurePercents;
        static int counterDummy;

        public WindowExecutionSubscriber(ILogger<WindowExecutionSubscriber> logger, int simulateFailurePercents, TimeSpan simulateExecutionTime)
        : base(simulateFailurePercents, simulateExecutionTime, logger)
        {
            _logger = logger;
            _simulateFailurePercents = simulateFailurePercents;
        }
        protected override async Task<HandlerResponse> handlePrivate(JobWindow<JobCustomData> msg)
        {
            var result = await runRuleCondition(msg);
            HandlerResponse handlerResult;
            if (result.conditionMet)
            {
                handlerResult = new HandlerResponse
                {
                    ResultStatusCode = 200,
                    ContinueWithResult = new HandlerResponse.ContinueWith
                    {
                        Message = new JobOutput { Id = Guid.NewGuid().ToString(), Name = "", JobSource = msg, WindowId = msg.WindowId, WindowFromTime = msg.FromTime, WindowToTime = msg.ToTime, },
                        TopicToPublish = Topics.JobWindowConditionMet.ToString()
                    }
                };
            }
            else
            {
                var execContext = msg.ToJson().FromJson<JobWindowExecutionContext>();
                execContext.WindowExecutionTime = DateTime.UtcNow;

                handlerResult = new HandlerResponse
                {
                    ResultStatusCode = 200,
                    ContinueWithResult = new HandlerResponse.ContinueWith
                    {
                        Message = execContext,
                        TopicToPublish = Topics.JobWindowConditionNotMet.ToString()
                    }
                };
            }

            return handlerResult;
        }

        private Task<(bool conditionMet, object result)> runRuleCondition(JobWindow<JobCustomData> msg)
        {
            counterDummy++;
            _logger.LogWarning($"Simulating window call {msg.FromTime:hh:mm:ss}-{msg.ToTime:hh:mm:ss} call to long unstable dependency for JobId {msg.Id} call: #{counterDummy}");

            var conditionMet = !msg.Schedule.PeriodicJob || (DateTime.UtcNow - msg.ToTime).TotalSeconds > 90;

            //   if (counterDummy % 2 == 0)
            //   {
            //return Task.FromResult<(bool conditionMet, object result)>((conditionMet: true, result: null));
            return Task.FromResult<(bool conditionMet, object result)>((conditionMet: conditionMet, result: null));
            //   }
            //   return (conditionMet: false, result: null);
        }
    }
}
