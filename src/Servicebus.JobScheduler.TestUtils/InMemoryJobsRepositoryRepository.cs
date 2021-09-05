using Servicebus.JobScheduler.ExampleApp.Common;
using Servicebus.JobScheduler.ExampleApp.Messages;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Servicebus.JobScheduler.ExampleApp.Emulators
{
    public class InMemoryJobsRepository : IRepository<JobDefinition>
    {
        readonly ConcurrentDictionary<string, JobDefinition> _db = new();
        public Task<JobDefinition> GetById(string id)
        {
            _db.TryGetValue(id, out var rule);

            return Task.FromResult(rule);
        }

        public Task<JobDefinition> Upsert(JobDefinition rule)
        {
            _db.TryRemove(rule.Id, out var _);
            rule.Etag = Guid.NewGuid().ToString();
            rule.JobDefinitionChangeTime = DateTime.UtcNow;
            _db.TryAdd(rule.Id, rule);
            return Task.FromResult(rule);
        }
    }
}