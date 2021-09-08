using Microsoft.Extensions.Logging;
using Servicebus.JobScheduler.Core.Contracts;
using Servicebus.JobScheduler.Core.Contracts.Messages;
using System;
using System.Threading.Tasks;

namespace Servicebus.JobScheduler.Core
{
    internal class WindowValidatorSubscriber<TJobPayload> : IMessageHandler<JobWindow<TJobPayload>>
    {
        private readonly IJobChangeProvider _changeDetector;
        private readonly ILogger _logger;

        public WindowValidatorSubscriber(ILogger<WindowValidatorSubscriber<TJobPayload>> logger, IJobChangeProvider changeProvider)
        {
            _changeDetector = changeProvider;

            _logger = logger;
        }

        public async Task<HandlerResponse> Handle(JobWindow<TJobPayload> msg)
        {
            // if (msg.RunId != _runId)
            // {
            //     _logger.LogDebug("TEST ONLY We ignore other runs messages");
            //     return HandlerResponse.FinalOk;
            // }

            // if (msg.WindowTimeRange <= TimeSpan.Zero)
            // {
            //     _logger.LogCritical("Invalid Interval for rule!");
            //     return HandlerResponse<Topics>.FinalOk;
            // }

            var handlerResult = HandlerResponse.FinalOk;

            if (msg.SkipNextWindowValidation)
            {
                handlerResult = new HandlerResponse { ResultStatusCode = 200, ContinueWithResult = new HandlerResponse.ContinueWith { Message = msg, TopicToPublish = SchedulingTopics.JobWindowValid.ToString() } };
            }
            else
            {
                var changeType = await _changeDetector.GetJobChangeType(msg.RuleId, msg.Etag);
                if (changeType == ChangeType.Changed || changeType == ChangeType.Deleted)
                {
                    // rule was changed, dont re-schedule another loop will handle this..
                    // also catches enable/disable of rules
                    _logger.LogInformation($"rule {msg.Id} was {changeType}, dont re-schedule another loop will handle this..");
                    handlerResult = new HandlerResponse { ResultStatusCode = 409 };
                }
                else
                {
                    _logger.LogInformation($"rule {msg.Id} Valid LastRunWindowUpperBound:{msg.LastRunWindowUpperBound}");
                    handlerResult = new HandlerResponse { ResultStatusCode = 200, ContinueWithResult = new HandlerResponse.ContinueWith { Message = msg, TopicToPublish = SchedulingTopics.JobWindowValid.ToString() } };
                }
            }
            return handlerResult;
        }


    }
}
