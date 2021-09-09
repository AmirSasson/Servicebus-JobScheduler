using System;
using Servicebus.JobScheduler.Core.Contracts;
using Servicebus.JobScheduler.ExampleApp.Common;

namespace Servicebus.JobScheduler.ExampleApp.Messages
{
    public class JobWindowExecutionContext : JobWindow<JobCustomData>
    {
        public DateTime WindowExecutionTime { get; set; }
    }
}

