using Servicebus.JobScheduler.Core.Contracts;
using Servicebus.JobScheduler.Core.Contracts.Messages;
using Servicebus.JobScheduler.ExampleApp.Common;
using Servicebus.JobScheduler.ExampleApp.Messages;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Servicebus.JobScheduler.ExampleApp.Emulators
{
    public class InMemoryJobsRepository : IRepository<Job<JobCustomData>>, Core.Contracts.Messages.IJobChangeProvider
    {
        readonly ConcurrentDictionary<string, Job<JobCustomData>> _db = new();
        public Task<Job<JobCustomData>> GetById(string id)
        {
            _db.TryGetValue(id, out var rule);

            return Task.FromResult(rule);
        }

        public async Task<ChangeType> GetJobChangeType(string jobId, string etag)
        {
            var mostUpdatedJob = await GetById(jobId);
            if (mostUpdatedJob == null)
            {
                return ChangeType.Deleted;
            }
            return mostUpdatedJob.Etag != etag ? ChangeType.Changed : ChangeType.NotChanged;
        }

        public Task<Job<JobCustomData>> Upsert(Job<JobCustomData> rule)
        {
            _db.TryRemove(rule.Id, out var _);
            rule.Etag = Guid.NewGuid().ToString();
            rule.JobDefinitionChangeTime = DateTime.UtcNow;
            _db.TryAdd(rule.Id, rule);
            return Task.FromResult(rule);
        }
    }
}