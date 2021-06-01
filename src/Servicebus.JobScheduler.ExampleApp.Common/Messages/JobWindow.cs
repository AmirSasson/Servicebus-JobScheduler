using System;

namespace Servicebus.JobScheduler.ExampleApp.Messages
{
    public class JobWindow : JobDefinition
    {
        public DateTime FromTime { get; set; }
        public DateTime ToTime { get; set; }
    }
}

