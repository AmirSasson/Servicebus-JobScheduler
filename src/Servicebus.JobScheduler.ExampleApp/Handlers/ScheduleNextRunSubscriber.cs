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
            //_scheduleForImmediate = scheduleForImmediate;
            //_repo = repo;
            //_runId = runId;
            _logger = logger;
        }

        protected override async Task<bool> handlePrivate(JobDefinition msg)
        {
            var shouldScheduleNextWindow = msg.RunInIntervals;
            _logger.LogInformation($"handling JobDefination should reschedule for later: {shouldScheduleNextWindow}");
            if (shouldScheduleNextWindow)
            {
                await publishWindowReady(msg);
            }
            return true;
        }

        /// <summary>
        /// publishes to WindowReady topic
        /// </summary>
        /// <param name="msg">the Job Defination</param>
        /// <param name="executionDelay">false if no need for aother rescheduleing (30 minutes ingestion time scenrio)</param>
        /// <param name="runInIntervals">false if no need for aother rescheduleing (30 minutes ingestion time scenrio)</param>
        /// <returns></returns>
        private async Task publishWindowReady(JobDefinition msg, TimeSpan? executionDelay = null, bool runInIntervals = true)
        {
            var nextWindowFromTime = msg.LastRunWindowUpperBound;
            var nextWindowToTime = msg.LastRunWindowUpperBound.Add(msg.WindowTimeRange);

            if (!string.IsNullOrEmpty(msg.CronSchedulingExpression))
            {
                nextWindowToTime = NCrontab.CrontabSchedule.Parse(msg.CronSchedulingExpression).GetNextOccurrence(msg.LastRunWindowUpperBound);
            }

            var window = new JobWindow //TODO: Auto mapper
            {
                Id = $"{nextWindowFromTime:HH:mm:ss}-{nextWindowToTime:HH:mm:ss}#{msg.RuleId}",
                WindowTimeRangeSeconds = msg.WindowTimeRangeSeconds,
                Name = "",
                RuleId = msg.RuleId,
                CronSchedulingExpression = msg.CronSchedulingExpression,
                FromTime = nextWindowFromTime,
                ToTime = nextWindowToTime,
                RunInIntervals = runInIntervals,
                Etag = msg.Etag,
                RunId = msg.RunId,
                LastRunWindowUpperBound = nextWindowToTime,
                JobDefinitionChangeTime = msg.JobDefinitionChangeTime,
                Status = msg.Status,
                BehaviorMode = msg.BehaviorMode,
                SkipValidation = false
            };
            await _bus.PublishAsync(window, Topics.ReadyToRunJobWindow, window.ToTime.Add(executionDelay ?? TimeSpan.Zero));
        }
    }
}
