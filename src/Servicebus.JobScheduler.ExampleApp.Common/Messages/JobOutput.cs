using Servicebus.JobScheduler.Core.Contracts.Messages;

namespace Servicebus.JobScheduler.ExampleApp.Messages
{
    public class JobOutput : BaseMessage
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string WindowId { get; set; }
        public string RuleId { get; set; }
        public JobDefinition Rule { get; set; }
        public string RunId { get; set; }

    }
}

