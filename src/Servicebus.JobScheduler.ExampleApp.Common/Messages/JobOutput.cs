using Servicebus.JobScheduler.Core.Contracts;
using Servicebus.JobScheduler.Core.Contracts.Messages;
using Servicebus.JobScheduler.ExampleApp.Common;

namespace Servicebus.JobScheduler.ExampleApp.Messages
{
    public class JobOutput : BaseJob
    {
        public string Name { get; set; }
        public string WindowId { get; set; }
        public Job<JobCustomData> JobSource { get; set; }
    }
}

