using Servicebus.JobScheduler.Core.Contracts.Messages;
using System;
using System.Text.Json.Serialization;

namespace Servicebus.JobScheduler.ExampleApp.Messages
{
    public class JobDefinition : IMessageBase
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int WindowTimeRangeSeconds { get; set; }
        [JsonIgnore]
        public TimeSpan WindowTimeRange => TimeSpan.FromSeconds(WindowTimeRangeSeconds);

        /// <summary>
        /// Cron expression, setting this property, will override the interval params.
        /// <br/>on first run, will run from now till nearest next schedule time
        /// <code>examples */5 * * * *</code>
        /// </summary>
        public string CronSchedulingExpression { get; set; }
        public bool RunInIntervals { get; set; }
        public string RuleId { get; set; }
        public string Etag { get; set; }
        public string RunId { get; set; }
        public DateTime LastRunWindowUpperBound { get; set; }

        /// <summary>
        /// this is just for testing
        /// </summary>
        public DateTime JobDefinitionChangeTime { get; set; }

        public JobStatus Status { get; set; }
        public JobBehaviorMode BehaviorMode { get; set; }
        public bool SkipValidation { get; set; }

        public enum JobStatus
        {
            Enabled,
            Disabled,
            Deleted
        }

        public enum JobBehaviorMode
        {
            Simple,
            DisabledAfterFirstJobOutput,
        }
    }
}

