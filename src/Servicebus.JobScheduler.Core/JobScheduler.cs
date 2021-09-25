

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Servicebus.JobScheduler.Core.Contracts;
using Servicebus.JobScheduler.Core.Contracts.Messages;

namespace Servicebus.JobScheduler.Core
{
    public class JobScheduler : IJobScheduler
    //where TTopics : struct, Enum where TSubscription : struct, Enum
    {
        private readonly IMessageBus _pubSubProvider;

        public JobScheduler(IMessageBus pubSubProvider, ILogger<JobScheduler> logger)
        {
            _pubSubProvider = pubSubProvider;
        }

        public async ValueTask DisposeAsync()
        {
            if (_pubSubProvider != null)
            {
                await _pubSubProvider.DisposeAsync();
            }
        }

        public async Task ScheduleJob<TJobPayload>(Job<TJobPayload> job)
        {
            job.JobType = EntitiesPathHelper.JobTypeName<TJobPayload>(); ;
            await _pubSubProvider.PublishAsync(job, SchedulingTopics.JobScheduled.ToString());
        }

        public async Task ScheduleOnce<TJobPayload>(Job<TJobPayload> job, DateTime? executeOnUtc = null)
        {
            job.JobType = EntitiesPathHelper.JobTypeName<TJobPayload>(); ;
            job.Schedule.PeriodicJob = false;
            await _pubSubProvider.PublishAsync(job, SchedulingTopics.JobInstanceReadyToRun.ToString(), executeOnUtc);
        }

        internal async Task SetupEntities(IEnumerable<string> topicsNames, IEnumerable<string> subscriptionNames)
        {
            await _pubSubProvider.SetupEntitiesIfNotExist(topicsNames, subscriptionNames);
        }

        internal async Task StartSchedulingWorkers(ILoggerFactory loggerFactory, IJobChangeProvider changeProvider, CancellationTokenSource source)
        {
            await _pubSubProvider.RegisterSubscriber(
               SchedulingTopics.JobScheduled.ToString(),
               SchedulingSubscriptions.JobScheduled_CreateJobWindowInstance.ToString(),
               concurrencyLevel: 3,
               new ScheduleNextRunSubscriber(loggerFactory.CreateLogger<ScheduleNextRunSubscriber>()),
               null,
               source);

            await _pubSubProvider.RegisterSubscriber(
               SchedulingTopics.JobInstanceReadyToRun.ToString(),
               SchedulingSubscriptions.JobInstanceReadyToRun_Validation.ToString(),
               concurrencyLevel: 3,
               new WindowValidatorSubscriber(loggerFactory.CreateLogger<WindowValidatorSubscriber>(), changeProvider),
               new RetryPolicy { PermanentErrorsTopic = SchedulingTopics.PermanentSchedulingErrors.ToString(), RetryDefinition = new RetryExponential(TimeSpan.FromSeconds(40), TimeSpan.FromMinutes(2), 3) },
               source);
        }

    }
}