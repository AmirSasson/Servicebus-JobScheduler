using Microsoft.Extensions.Logging;
using Servicebus.JobScheduler.Core;
using Servicebus.JobScheduler.Core.Contracts;
using Servicebus.JobScheduler.ExampleApp.Common;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Servicebus.JobScheduler.ExampleApp.Emulators
{
    public class UpsertsClientSimulator
    {
        public static async Task Run(IJobScheduler<JobCustomData> scheduler, int initialRuleCount, TimeSpan delayBetweenUpserts, int maxConcurrentRules, IRepository<Job<JobCustomData>> repo, ILogger logger, int? maxUpserts, TimeSpan ruleInterval, CancellationToken token)
        {
            var totalRulesCount = 0;
            logger.LogInformation($"Running Tester Client");
            while (!token.IsCancellationRequested && (!maxUpserts.HasValue || maxUpserts.Value > totalRulesCount))
            {
                var id = $"Simulator2@{(totalRulesCount % maxConcurrentRules)}";
                var rule = new Job<JobCustomData>
                {
                    Id = id,
                    RuleId = id,
                    Payload = new JobCustomData { Query = "test Query", BehaviorMode = JobCustomData.JobBehaviorMode.Simple },
                    //WindowTimeRangeSeconds = (int)ruleInterval.TotalSeconds,
                    //Schedule = new JobSchedule { PeriodicJob = true, RunIntervalSeconds = (int)ruleInterval.TotalSeconds },
                    Schedule = new JobSchedule { PeriodicJob = true, CronSchedulingExpression = "*/3 * * * *" },
                    //Schedule = new JobSchedule { PeriodicJob = true, CronSchedulingExpression = "*/5 * * * *", RunIntervalSeconds = 6 * 60 },

                    LastRunWindowUpperBound = null,//
                    SkipNextWindowValidation = true,
                    Status = JobStatus.Enabled
                };
                logger.LogInformation($"****************************");
                logger.LogCritical($"TEST - Upserting Rule {rule.Id}");
                logger.LogInformation($"****************************");

                var savedRule = await repo.Upsert(rule);
                await scheduler.ScheduleJob(savedRule);

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
