using Microsoft.Extensions.Logging;
using Servicebus.JobScheduler.Core.Contracts;
using Servicebus.JobScheduler.ExampleApp.Common;
using Servicebus.JobScheduler.ExampleApp.Messages;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Servicebus.JobScheduler.ExampleApp.Emulators
{
    public class UpsertsClientSimulator
    {
        public static async Task Run(IMessageBus<Topics, Subscriptions> bus, int initialRuleCount, TimeSpan delayBetweenUpserts, int maxConcurrentRules, string runId, IRepository<JobDefinition> repo, ILogger logger, int? maxUpserts, TimeSpan ruleInterval, CancellationToken token)
        {
            var totalRulesCount = 0;
            logger.LogInformation($"Running Tester Client");
            while (!token.IsCancellationRequested && (!maxUpserts.HasValue || maxUpserts.Value > totalRulesCount))
            {
                var id = $"{runId}@{(totalRulesCount % maxConcurrentRules)}";
                var rule = new JobDefinition
                {
                    Id = id.ToString(),
                    Name = $"TestRule {id}",
                    WindowTimeRangeSeconds = (int)ruleInterval.TotalSeconds,
                    Schedule = new JobSchedule { PeriodicJob = true, RunIntervalSeconds = (int)ruleInterval.TotalSeconds },
                    //Schedule = new JobSchedule { PeriodicJob = true, CronSchedulingExpression = "*/5 * * * *" },
                    RuleId = id.ToString(),
                    RunId = runId,
                    LastRunWindowUpperBound = DateTime.UtcNow.Subtract(ruleInterval), // so immediate schedule will run on current window
                    JobDefinitionChangeTime = DateTime.UtcNow,
                    SkipNextWindowValidation = true,
                    //BehaviorMode = JobDefination.RuleBehaviorMode.DisabledAfterFirstJobOutput,
                    BehaviorMode = JobDefinition.JobBehaviorMode.Simple,
                    Status = JobStatus.Enabled
                };
                logger.LogInformation($"****************************");
                logger.LogCritical($"TEST - Upserting Rule {rule.RuleId}");
                logger.LogInformation($"****************************");

                var savedRule = await repo.Upsert(rule);
                await bus.PublishAsync(savedRule, Topics.JobDefinitionChange);

                totalRulesCount++;
                if (totalRulesCount >= initialRuleCount)
                {
                    await Task.Delay(delayBetweenUpserts, token);
                }
            }
            logger.LogInformation("Tester Client existed");
        }
    }
}
