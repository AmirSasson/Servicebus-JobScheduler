using Microsoft.Extensions.Logging;
using Servicebus.JobScheduler.Core.Contracts;
using System;
using System.Threading.Tasks;

namespace Servicebus.JobScheduler.Core
{
    internal class ScheduleNextRunSubscriber<TJobPayload> : IMessageHandler<Job<TJobPayload>>
    {
        private readonly ILogger _logger;

        public ScheduleNextRunSubscriber(ILogger<ScheduleNextRunSubscriber<TJobPayload>> logger)
        {
            _logger = logger;
        }

        public Task<HandlerResponse> Handle(Job<TJobPayload> msg)
        {
            var shouldScheduleNextWindow = msg.Schedule.PeriodicJob;
            _logger.LogInformation($"handling JobDefinition should reschedule for later: {shouldScheduleNextWindow}");
            if (shouldScheduleNextWindow)
            {
                var nextJob = publishWindowReady(msg);
                JobWindow<TJobPayload> job = nextJob.ContinueWithResult.Message as JobWindow<TJobPayload>;
                _logger.LogInformation($"Scheduling Next window: {job.FromTime} -> {job.ToTime} -> executed on {nextJob.ContinueWithResult.ExecuteOnUtc}");
                return nextJob.AsTask();
            }
            return HandlerResponse.FinalOkAsTask;
        }

        /// <summary>
        /// publishes to WindowReady topic
        /// </summary>
        /// <param name="msg">the Job Defination</param>
        /// <param name="executionDelay">false if no need for aother rescheduleing (30 minutes ingestion time scenrio)</param>
        /// <param name="runInIntervals">false if no need for aother rescheduleing (30 minutes ingestion time scenrio)</param>
        /// <returns></returns>
        private HandlerResponse publishWindowReady(Job<TJobPayload> msg, bool runInIntervals = true)
        {
            (DateTime? nextWindowFromTime, DateTime? nextWindowToTime) = msg.Schedule.GetNextScheduleWindowTimeRange(msg.LastRunWindowUpperBound);

            if (nextWindowToTime.HasValue)
            {
                var window = new JobWindow<TJobPayload> //TODO: Auto mapper
                {
                    Id = $"{nextWindowFromTime:HH:mm:ss}-{nextWindowToTime:HH:mm:ss}#{msg.RuleId}",
                    Payload = msg.Payload,
                    RuleId = msg.RuleId,
                    Schedule = msg.Schedule,
                    FromTime = nextWindowFromTime.Value,
                    ToTime = nextWindowToTime.Value,
                    Etag = msg.Etag,
                    LastRunWindowUpperBound = nextWindowToTime.Value,
                    JobDefinitionChangeTime = msg.JobDefinitionChangeTime,
                    Status = msg.Status,
                    SkipNextWindowValidation = msg.Schedule.ForceSuppressWindowValidation || false,
                };
                var executionDelay = msg.Schedule.RunDelayUponDueTimeSeconds.HasValue ? TimeSpan.FromSeconds(msg.Schedule.RunDelayUponDueTimeSeconds.Value) : TimeSpan.Zero;
                return new HandlerResponse { ResultStatusCode = 200, ContinueWithResult = new HandlerResponse.ContinueWith { Message = window, TopicToPublish = SchedulingTopics.JobInstanceReadyToRun.ToString(), ExecuteOnUtc = window.ToTime.Add(executionDelay) } };
            }
            return HandlerResponse.FinalOk;
        }
    }
}
