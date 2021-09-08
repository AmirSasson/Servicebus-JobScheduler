namespace Servicebus.JobScheduler.ExampleApp.Common
{
    public class JobCustomData
    {
        public string Query { get; set; }

        public JobBehaviorMode BehaviorMode { get; set; }

        public enum JobBehaviorMode
        {
            Simple,
            DisabledAfterFirstJobOutput,
        }

    }
}