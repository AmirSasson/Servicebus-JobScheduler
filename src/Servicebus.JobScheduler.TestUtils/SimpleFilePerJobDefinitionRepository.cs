using Servicebus.JobScheduler.Core.Contracts;
using Servicebus.JobScheduler.Core.Contracts.Messages;
using Servicebus.JobScheduler.ExampleApp.Common;
using Servicebus.JobScheduler.ExampleApp.Messages;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Servicebus.JobScheduler.ExampleApp.Emulators
{
    public class SimpleFilePerJobDefinitionRepository : IRepository<Job<JobCustomData>>, Core.Contracts.Messages.IJobChangeProvider
    {
        private readonly string _folder;

        public SimpleFilePerJobDefinitionRepository(string folder)
        {
            _folder = folder;
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
        }
        public Task<Job<JobCustomData>> GetById(string id)
        {
            Job<JobCustomData> rule = null;
            if (File.Exists(ruleFileName(id)))
            {
                var str = File.ReadAllText(ruleFileName(id));
                rule = JsonSerializer.Deserialize<Job<JobCustomData>>(str);
            }
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
            if (File.Exists(ruleFileName(rule.Id)))
            {
                File.Delete(ruleFileName(rule.Id));
            }
            rule.Etag = Guid.NewGuid().ToString();
            rule.JobDefinitionChangeTime = DateTime.UtcNow;
            File.WriteAllText(ruleFileName(rule.Id), JsonSerializer.Serialize(rule));
            return Task.FromResult(rule);
        }
        private string ruleFileName(string ruleId) => $"{_folder}/{ruleId}.rule";
    }
}