

using System;
using System.Threading.Tasks;
using Servicebus.JobScheduler.Core.Contracts;

namespace Servicebus.JobScheduler.Core
{
    public interface IJobScheduler : IAsyncDisposable
    {
        Task ScheduleJob<TJobPayload>(Job<TJobPayload> job);

        Task ScheduleOnce<TJobPayload>(Job<TJobPayload> job, DateTime? executeOnUtc = null);
    }
}