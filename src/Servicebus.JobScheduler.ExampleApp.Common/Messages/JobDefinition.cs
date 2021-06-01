using Servicebus.JobScheduler.Core.Contracts.Messages;
using System;
using System.Text.Json.Serialization;

namespace Servicebus.JobScheduler.ExampleApp.Messages
{
    public class JobDefinition : IMessageBase
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int IntervalSeconds { get; set; }
        [JsonIgnore]
        public TimeSpan Interval => TimeSpan.FromSeconds(IntervalSeconds);

        public bool RunInIntervals { get; set; }
        public string RuleId { get; set; }
        public string Etag { get; set; }
        public string RunId { get; set; }
        public DateTime LastRunWindowUpperBound { get; set; }

        /// <summary>
        /// this is just for testing
        /// </summary>
        public DateTime ChangeTime { get; set; }

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

