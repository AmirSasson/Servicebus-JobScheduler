using Servicebus.JobScheduler.ExampleApp.Common;
using Servicebus.JobScheduler.ExampleApp.Messages;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Servicebus.JobScheduler.ExampleApp.Emulators
{
    public class SimpleFilePerJobDefinitionRepository : IRepository<JobDefinition>
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
        public Task<JobDefinition> GetById(string id)
        {
            JobDefinition rule = null;
            if (File.Exists(ruleFileName(id)))
            {
                var str = File.ReadAllText(ruleFileName(id));
                rule = JsonSerializer.Deserialize<JobDefinition>(str);
            }
            return Task.FromResult(rule);
        }

        public Task<JobDefinition> Upsert(JobDefinition rule)
        {
            if (File.Exists(ruleFileName(rule.RuleId)))
            {
                File.Delete(ruleFileName(rule.RuleId));
            }
            rule.Etag = Guid.NewGuid().ToString();
            rule.JobDefinitionChangeTime = DateTime.UtcNow;
            File.WriteAllText(ruleFileName(rule.RuleId), JsonSerializer.Serialize(rule));
            return Task.FromResult(rule);
        }
        private string ruleFileName(string ruleId) => $"{_folder}/{ruleId}.rule";
    }
}