

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Servicebus.JobScheduler.Core.Contracts;
using Servicebus.JobScheduler.Core.Contracts.Messages;

namespace Servicebus.JobScheduler.Core
{
    public class JobScheduler<TJobPayload> : IJobScheduler<TJobPayload>
    //where TTopics : struct, Enum where TSubscription : struct, Enum
    {
        private readonly IMessageBus _pubSubProvider;

        public JobScheduler(IMessageBus pubSubProvider, ILogger<JobScheduler<TJobPayload>> logger)
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

        public async Task ScheduleJob(Job<TJobPayload> job)
        {
            await _pubSubProvider.PublishAsync(job, SchedulingTopics.JobScheduled.ToString());
        }

        public async Task ScheduleOnce(Job<TJobPayload> job, DateTime? executeOnUtc = null)
        {
            job.Schedule.PeriodicJob = false;
            await _pubSubProvider.PublishAsync(job, SchedulingTopics.JobInstanceReadyToRun.ToString(), executeOnUtc);
        }

        internal async Task SetupEntities(IConfiguration config, IEnumerable<string> topicsNames, IEnumerable<string> subscriptionNames)
        {
            await _pubSubProvider.SetupEntitiesIfNotExist(config, topicsNames, subscriptionNames);
        }

        internal async Task StartSchedulingWorkers(IConfiguration config, ILoggerFactory loggerFactory, IJobChangeProvider changeProvider, CancellationTokenSource source)
        {
            await _pubSubProvider.RegisterSubscriber(
               SchedulingTopics.JobScheduled.ToString(),
               SchedulingSubscriptions.JobScheduled_CreateJobWindowInstance.ToString(),
               concurrencyLevel: 3,
               new ScheduleNextRunSubscriber<TJobPayload>(loggerFactory.CreateLogger<ScheduleNextRunSubscriber<TJobPayload>>()),
               null,
               source);


            await _pubSubProvider.RegisterSubscriber(
               SchedulingTopics.JobInstanceReadyToRun.ToString(),
               SchedulingSubscriptions.JobInstanceReadyToRun_Validation.ToString(),
               concurrencyLevel: 3,
               new WindowValidatorSubscriber<TJobPayload>(loggerFactory.CreateLogger<WindowValidatorSubscriber<TJobPayload>>(), changeProvider),
               new RetryPolicy { PermanentErrorsTopic = SchedulingTopics.PermanentSchedulingErrors.ToString(), RetryDefinition = new RetryExponential(TimeSpan.FromSeconds(40), TimeSpan.FromMinutes(2), 3) },
               source);



            await _pubSubProvider.RegisterSubscriber(
                SchedulingTopics.JobWindowValid.ToString(),
                SchedulingSubscriptions.JobWindowValid_ScheduleNextRun.ToString(),
                concurrencyLevel: 3,
                new ScheduleNextRunSubscriber<TJobPayload>(loggerFactory.CreateLogger<ScheduleNextRunSubscriber<TJobPayload>>()),
                null,
                source);
        }

    }
}