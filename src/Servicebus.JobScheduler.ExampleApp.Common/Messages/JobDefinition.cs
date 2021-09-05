using Servicebus.JobScheduler.Core.Contracts;
using Servicebus.JobScheduler.Core.Contracts.Messages;
using System;
using System.Text.Json.Serialization;

namespace Servicebus.JobScheduler.ExampleApp.Messages
{
    public class JobSchedule
    {
        /// <summary>
        /// Cron expression, setting this property, will override the interval params.
        /// <br/>on first run, will run from now till nearest next schedule time
        /// <code>examples */5 * * * *</code>
        /// </summary>
        public string CronSchedulingExpression { get; set; }

        /// <summary>
        /// When Null, it is calculated by window Range to avoid overlapping with other previous windows time ranges
        /// </summary>
        public int? RunIntervalSeconds { get; set; }

        /// <summary>
        /// Seconds to delay when window is due (mostly used when you want to stall the Job, when data is not ready/ingested yet)
        /// </summary>
        public int? RunDelayUponDueTimeSeconds { get; set; }

        /// <summary>
        /// is true, the job would run periodically (cron/every X seconds), if false the job would run one time.
        /// </summary>
        public bool PeriodicJob { get; set; }

        /// <summary>
        /// When Set, After this date, no more schedules and this Job wont be scheduled anymore
        /// </summary>
        public DateTime? ScheduleEndTime { get; set; }

        /// <summary>
        /// When True, All rule windows will be valid, must go with ScheduleEndTime
        /// </summary>
        public bool ForceSuppressWindowValidation { get; set; }

        /// <summary>
        /// returns next Scheduletime based on previous window Upper Bound
        /// </summary>
        public (DateTime? from, DateTime? to) GetNextScheduleWindowTimeRange(DateTime? previousRunUpperBound)
        {
            int actualRunIntervalSeconds = this.RunIntervalSeconds ?? getWindowRangeInSeconds();
            var actualPreviousRunUpperBound = previousRunUpperBound ?? DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(actualRunIntervalSeconds));
            var nextWindowLowerBoundTime = actualPreviousRunUpperBound;
            var nextWindowUpperBoundTime = actualPreviousRunUpperBound.Add(TimeSpan.FromSeconds(actualRunIntervalSeconds));

            if (!string.IsNullOrEmpty(CronSchedulingExpression))
            {
                nextWindowUpperBoundTime = NCrontab.CrontabSchedule.Parse(CronSchedulingExpression).GetNextOccurrence(actualPreviousRunUpperBound);
                nextWindowLowerBoundTime = nextWindowUpperBoundTime.Subtract(TimeSpan.FromSeconds(actualRunIntervalSeconds));
            }
            if (!ScheduleEndTime.HasValue || nextWindowUpperBoundTime <= ScheduleEndTime.Value)
            {
                return (nextWindowLowerBoundTime, nextWindowUpperBoundTime);
            }
            return (null, null);
        }

        private int getWindowRangeInSeconds()
        {
            if (!this.RunIntervalSeconds.HasValue && !string.IsNullOrEmpty(CronSchedulingExpression))
            {
                var cron = NCrontab.CrontabSchedule.Parse(CronSchedulingExpression);
                var occurrence1 = cron.GetNextOccurrence(DateTime.UtcNow);
                var occurrence2 = cron.GetNextOccurrence(occurrence1);
                var rangeInSec = (int)(occurrence2 - occurrence1).TotalSeconds;
                return rangeInSec;
            }
            else if (this.RunIntervalSeconds.HasValue)
            {
                return this.RunIntervalSeconds.Value;
            }
            throw new InvalidOperationException("Scheduled job must specify a scheduling method or time window range. either RunIntervalSeconds or CrontabSchedule must contains valid values");
        }
    }
    public class JobDefinition : BaseMessage
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string RuleId { get; set; }
        public string Etag { get; set; }
        public string RunId { get; set; }
        /// <summary>
        /// last run Window upper time bound
        /// </summary>
        public DateTime? LastRunWindowUpperBound { get; set; }

        // public int? WindowTimeRangeSeconds { get; set; }
        // [JsonIgnore]
        // public TimeSpan WindowTimeRange => TimeSpan.FromSeconds(WindowTimeRangeSeconds);

        public JobSchedule Schedule { get; set; }
        /// <summary>
        /// this is just for testing
        /// </summary>
        public DateTime JobDefinitionChangeTime { get; set; }

        public JobStatus Status { get; set; }
        public JobBehaviorMode BehaviorMode { get; set; }
        public bool SkipNextWindowValidation { get; set; }

        public enum JobBehaviorMode
        {
            Simple,
            DisabledAfterFirstJobOutput,
        }
    }
}

