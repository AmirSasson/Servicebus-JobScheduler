using Microsoft.Extensions.Logging;
using Servicebus.JobScheduler.Core.Contracts;
using Servicebus.JobScheduler.ExampleApp.Messages;
using System;
using System.Threading.Tasks;

namespace Servicebus.JobScheduler.ExampleApp.Handlers
{
    public class ScheduleNextRunSubscriber : BaseSimulatorHandler<JobDefinition>
    {
        private readonly IMessageBus<Topics, Subscriptions> _bus;
        private readonly ILogger _logger;

        public ScheduleNextRunSubscriber(IMessageBus<Topics, Subscriptions> bus, ILogger<ScheduleNextRunSubscriber> logger, int simulateFailurePercents)
        : base(simulateFailurePercents, TimeSpan.Zero, logger)
        {
            _bus = bus;
            _logger = logger;
        }

        protected override Task<HandlerResponse<Topics>> handlePrivate(JobDefinition msg)
        {
            var shouldScheduleNextWindow = msg.Schedule.PeriodicJob;
            _logger.LogInformation($"handling JobDefinition should reschedule for later: {shouldScheduleNextWindow}");
            if (shouldScheduleNextWindow)
            {
                return publishWindowReady(msg).AsTask();
            }
            return HandlerResponse<Topics>.FinalOkAsTask;
        }

        /// <summary>
        /// publishes to WindowReady topic
        /// </summary>
        /// <param name="msg">the Job Defination</param>
        /// <param name="executionDelay">false if no need for aother rescheduleing (30 minutes ingestion time scenrio)</param>
        /// <param name="runInIntervals">false if no need for aother rescheduleing (30 minutes ingestion time scenrio)</param>
        /// <returns></returns>
        private HandlerResponse<Topics> publishWindowReady(JobDefinition msg, bool runInIntervals = true)
        {
            var nextWindowFromTime = msg.LastRunWindowUpperBound;
            DateTime? nextWindowToTime = msg.Schedule.GetNextScheduleUpperBoundTime(msg.LastRunWindowUpperBound);

            if (nextWindowToTime.HasValue)
            {
                var window = new JobWindow //TODO: Auto mapper
                {
                    Id = $"{nextWindowFromTime:HH:mm:ss}-{nextWindowToTime:HH:mm:ss}#{msg.RuleId}",
                    WindowTimeRangeSeconds = msg.WindowTimeRangeSeconds,
                    Name = "",
                    RuleId = msg.RuleId,
                    Schedule = msg.Schedule,
                    FromTime = nextWindowFromTime,
                    ToTime = nextWindowToTime.Value,
                    Etag = msg.Etag,
                    RunId = msg.RunId,
                    LastRunWindowUpperBound = nextWindowToTime.Value,
                    JobDefinitionChangeTime = msg.JobDefinitionChangeTime,
                    Status = msg.Status,
                    BehaviorMode = msg.BehaviorMode,
                    SkipNextWindowValidation = msg.Schedule.ForceSuppressWindowValidation || false,
                };
                var executionDelay = msg.Schedule.RunDelayUponDueTimeSeconds.HasValue ? TimeSpan.FromSeconds(msg.Schedule.RunDelayUponDueTimeSeconds.Value) : TimeSpan.Zero;
                return new HandlerResponse<Topics> { ResultStatusCode = 200, ContinueWithResult = new HandlerResponse<Topics>.ContinueWith { Message = window, TopicToPublish = Topics.ReadyToRunJobWindow, ExecuteOnUtc = window.ToTime.Add(executionDelay) } };
            }
            return HandlerResponse<Topics>.FinalOk;
        }
    }
}
