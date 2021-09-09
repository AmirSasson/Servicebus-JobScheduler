

using System;
using System.Threading.Tasks;
using Servicebus.JobScheduler.Core.Contracts;

namespace Servicebus.JobScheduler.Core
{
    public interface IJobScheduler<TJobPayload> : IAsyncDisposable
    {
        Task ScheduleJob(Job<TJobPayload> job);

        Task ScheduleOnce(Job<TJobPayload> job, DateTime? executeOnUtc = null);
    }
}