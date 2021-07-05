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

        public int RunIntervalSeconds { get; set; }

        /// <summary>
        /// Seconds to delay when window is due
        /// </summary>
        public int? RunDelayUponDueTimeSeconds { get; set; }

        /// <summary>
        /// is this job should run periodically (cron/every X seconds)
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
        public DateTime? GetNextScheduleUpperBoundTime(DateTime previousRunUpperBound)
        {
            var nextWindowToTime = previousRunUpperBound.Add(TimeSpan.FromSeconds(this.RunIntervalSeconds));

            if (!string.IsNullOrEmpty(CronSchedulingExpression))
            {
                nextWindowToTime = NCrontab.CrontabSchedule.Parse(CronSchedulingExpression).GetNextOccurrence(previousRunUpperBound);
            }
            if (!ScheduleEndTime.HasValue || nextWindowToTime <= ScheduleEndTime.Value)
            {
                return nextWindowToTime;
            }
            return null;
        }
    }
    public class JobDefinition : IMessageBase
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string RuleId { get; set; }
        public string Etag { get; set; }
        public string RunId { get; set; }
        public DateTime LastRunWindowUpperBound { get; set; }

        public int WindowTimeRangeSeconds { get; set; }
        [JsonIgnore]
        public TimeSpan WindowTimeRange => TimeSpan.FromSeconds(WindowTimeRangeSeconds);

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

