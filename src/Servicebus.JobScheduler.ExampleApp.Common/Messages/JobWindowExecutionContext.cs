using System;

namespace Servicebus.JobScheduler.ExampleApp.Messages
{
    public class JobWindowExecutionContext : JobWindow
    {
        public DateTime WindowExecutionTime { get; set; }
    }
}

