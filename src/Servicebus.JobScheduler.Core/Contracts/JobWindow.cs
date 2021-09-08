using System;

namespace Servicebus.JobScheduler.Core.Contracts
{
    public class JobWindow<TJobPayload> : Job<TJobPayload>
    {
        public DateTime FromTime { get; set; }
        public DateTime ToTime { get; set; }
        public string WindowId => $"{base.Id}[{FromTime}->{ToTime}]";
    }
}

