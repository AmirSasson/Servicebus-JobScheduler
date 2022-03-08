using System.Collections.Generic;

namespace Servicebus.JobScheduler.ExampleApp.Common
{
    public class JobCustomData
    {
        public string Query { get; set; }
        public ComplexCustomData Complex { get; set; }

        public JobBehaviorMode BehaviorMode { get; set; }

        public enum JobBehaviorMode
        {
            Simple,
            DisabledAfterFirstJobOutput,
        }
    }

    public class ComplexCustomData
    {
        public List<int> SomeList { get; set; }

        public Dictionary<string, object> SomeDic { get; set; }
    }
}