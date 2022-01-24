

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Servicebus.JobScheduler.Core.Bus;
using Servicebus.JobScheduler.Core.Bus.Emulator;
using Servicebus.JobScheduler.Core.Contracts;
using Servicebus.JobScheduler.Core.Contracts.Messages;
using Microsoft.Extensions.DependencyInjection;
using Servicebus.JobScheduler.Core.Utils;

namespace Servicebus.JobScheduler.Core
{
    public class JobSchedulerBuilder
    {
        private Contracts.IMessageBus _pubSubProvider;
        private ILoggerFactory _logger = null;
        private IOptions<ServiceBusConfig> _config;
        private bool _initiateSchedulingWorkers = true;
        private CancellationTokenSource _source;
        private IJobChangeProvider _changeProvider = new EmptyChangeProvider();
        private List<Action> _preBuildActions = new();
        private List<Func<JobScheduler, Task>> _buildTasks = new();
        private IServiceProvider _serviceProvider;
        private HashSet<string> _scheduledJobTypes = new HashSet<string>();
        private readonly HashSet<string> _topics = Enum.GetNames<SchedulingTopics>().ToHashSet();
        private readonly HashSet<string> _subscriptions = Enum.GetNames<SchedulingSubscriptions>().ToHashSet();

        public JobSchedulerBuilder UsePubsubProvider(Contracts.IMessageBus pubSubProvider)
        {
            _pubSubProvider = pubSubProvider;
            return this;
        }
        public JobSchedulerBuilder UseLoggerFactory(ILoggerFactory logger)
        {
            _logger = logger;
            return this;
        }
        public JobSchedulerBuilder UseSchedulingWorker(bool initiateSchedulingWorkers = true)
        {
            _initiateSchedulingWorkers = initiateSchedulingWorkers;
            return this;
        }

        public JobSchedulerBuilder WithCancelationSource(CancellationTokenSource source)
        {
            _source = source;
            return this;
        }

        public JobSchedulerBuilder WithConfiguration(IOptions<ServiceBusConfig> config)
        {
            _config = config;
            return this;
        }

        public JobSchedulerBuilder UseJobChangeProvider(IJobChangeProvider changeProvider)
        {
            _changeProvider = changeProvider;
            return this;
        }

        public JobSchedulerBuilder WithHandlersServiceProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            return this;
        }

        public IJobScheduler Build()
        {
            return BuildAsync().Result;
        }
        public async Task<IJobScheduler> BuildAsync()
        {
            if (!_scheduledJobTypes.Any())
            {
                throw new InvalidProgramException("Root Handler must be registered!, Use AddRootJobExecuter");
            }
            Validator.EnsureAtLeastOneNotNull("Either Configuration or Service Provider must be specified", _config, _serviceProvider);
            _config = _config ?? _serviceProvider.GetService<IOptions<ServiceBusConfig>>();
            _logger = _logger ?? _serviceProvider.GetService<ILoggerFactory>() ?? new Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory();

            foreach (var action in _preBuildActions)
            {
                action();
            }
            
            _pubSubProvider = _pubSubProvider ?? new AzureServiceBusService(_config, _logger.CreateLogger<AzureServiceBusService>(), _serviceProvider);
            var scheduler = new JobScheduler(_pubSubProvider, _logger.CreateLogger<JobScheduler>());

            await scheduler.SetupEntities(_topics, _subscriptions);

            if (_initiateSchedulingWorkers)
            {
                await scheduler.StartSchedulingWorkers(_logger, _changeProvider, _source);
            }
            foreach (var task in _buildTasks)
            {
                await task(scheduler);
            }
            
            return scheduler;
        }

        public JobSchedulerBuilder AddRootJobExecuter<TJobPayload>(IMessageHandler<JobWindow<TJobPayload>> mainHandler, int concurrencyLevel, RetryPolicy retryPolicy, bool enabled = true)
        {
            var scheduledJobType = EntitiesPathHelper.JobTypeName<TJobPayload>();
            if (!_scheduledJobTypes.Add(scheduledJobType))
            {
                throw new InvalidProgramException($"Root with JobType {scheduledJobType} Handler already registered!, you may call AddRootJobExecuter only once per jobType");
            }
            var dynTopicName = EntitiesPathHelper.GetDynamicTopicName<TJobPayload>(SchedulingDynamicTopicsPrefix.JobWindowValid);
            var dynExecSubName = EntitiesPathHelper.GetDynamicSubscriptionName<TJobPayload>(SchedulingynamicSubscriptionsPrefix.JobWindowValid_SUBNAME__JobExecution);
            var dynSubSchedName = EntitiesPathHelper.GetDynamicSubscriptionName<TJobPayload>(SchedulingynamicSubscriptionsPrefix.JobWindowValid_SUBNAME__ScheduleNextRun);
            _topics.Add(dynTopicName);
            _subscriptions.Add(dynExecSubName);
            _subscriptions.Add(dynSubSchedName);
            if (retryPolicy != null && !string.IsNullOrWhiteSpace(retryPolicy.PermanentErrorsTopic))
            {
                _topics.Add(retryPolicy.PermanentErrorsTopic);
            }
            if (enabled)
            {
                _buildTasks.Add(async (_) =>
                {
                    await _pubSubProvider.RegisterSubscriber(
                        dynTopicName,
                        dynExecSubName,
                        concurrencyLevel: 3,
                        mainHandler,
                        retryPolicy,
                        _source);
                });

                _buildTasks.Add(async (_) =>
               {
                   await _pubSubProvider.RegisterSubscriber(
                   dynTopicName,
                   dynSubSchedName,
                   concurrencyLevel: 3,
                   new ScheduleNextRunSubscriber(_logger.CreateLogger<ScheduleNextRunSubscriber>()),
                   null,
                   _source);
               });
            }
            return this;
        }

        public JobSchedulerBuilder AddRootJobExecuterType<THandler, TJobPayload>(int concurrencyLevel, RetryPolicy retryPolicy, bool enabled = true)
            where THandler : IMessageHandler<JobWindow<TJobPayload>>
        {
            var scheduledJobType = EntitiesPathHelper.JobTypeName<TJobPayload>();
            if (!_scheduledJobTypes.Add(scheduledJobType))
            {
                throw new InvalidProgramException($"Root with JobType {scheduledJobType} Handler already registered!, you may call AddRootJobExecuter only once per jobType");
            }
            var dynTopicName = EntitiesPathHelper.GetDynamicTopicName<TJobPayload>(SchedulingDynamicTopicsPrefix.JobWindowValid);
            var dynExecSubName = EntitiesPathHelper.GetDynamicSubscriptionName<TJobPayload>(SchedulingynamicSubscriptionsPrefix.JobWindowValid_SUBNAME__JobExecution);
            var dynSubSchedName = EntitiesPathHelper.GetDynamicSubscriptionName<TJobPayload>(SchedulingynamicSubscriptionsPrefix.JobWindowValid_SUBNAME__ScheduleNextRun);
            _topics.Add(dynTopicName);
            _subscriptions.Add(dynExecSubName);
            _subscriptions.Add(dynSubSchedName);
            if (retryPolicy != null && !string.IsNullOrWhiteSpace(retryPolicy.PermanentErrorsTopic))
            {
                _topics.Add(retryPolicy.PermanentErrorsTopic);
            }
            if (enabled)
            {
                _buildTasks.Add(async (_) =>
                {
                    await _pubSubProvider.RegisterSubscriberType<JobWindow<TJobPayload>, THandler>(
                        dynTopicName,
                        dynExecSubName,
                        concurrencyLevel: 3,
                        retryPolicy,
                        _source);
                });

                _buildTasks.Add(async (_) =>
                {
                    await _pubSubProvider.RegisterSubscriber(
                    dynTopicName,
                    dynSubSchedName,
                    concurrencyLevel: 3,
                    new ScheduleNextRunSubscriber(_logger.CreateLogger<ScheduleNextRunSubscriber>()),
                    null,
                    _source);
                });
            }
            return this;
        }

        public JobSchedulerBuilder AddSubJobHandler<TMessageType>(string topic, string sub, IMessageHandler<TMessageType> handler, int concurrencyLevel, RetryPolicy retryPolicy = null, bool enabled = true) where TMessageType : class, IJob
        {
            if (retryPolicy != null && !string.IsNullOrWhiteSpace(retryPolicy.PermanentErrorsTopic))
            {
                _topics.Add(retryPolicy.PermanentErrorsTopic);
            }
            _topics.Add(topic);
            _subscriptions.Add(sub);
            if (enabled)
            {
                _buildTasks.Add(async (_) =>
                {
                    await _pubSubProvider.RegisterSubscriber(
                        topic,
                        sub,
                        concurrencyLevel,
                        handler,
                        retryPolicy,
                        _source);
                });
            }
            return this;
        }

        public JobSchedulerBuilder AddSubJobHandlerType<TMessageType, THandler>(string topic, string sub, int concurrencyLevel, RetryPolicy retryPolicy = null, bool enabled = true)
            where TMessageType : class, IJob
            where THandler : IMessageHandler<TMessageType>
        {
            if (retryPolicy != null && !string.IsNullOrWhiteSpace(retryPolicy.PermanentErrorsTopic))
            {
                _topics.Add(retryPolicy.PermanentErrorsTopic);
            }
            _topics.Add(topic);
            _subscriptions.Add(sub);
            if (enabled)
            {
                _buildTasks.Add(async (_) =>
                {
                    await _pubSubProvider.RegisterSubscriberType<TMessageType, THandler>(
                        topic,
                        sub,
                        concurrencyLevel,
                        retryPolicy,
                        _source);
                });
            }
            return this;
        }

        public JobSchedulerBuilder UseInMemoryPubsubProvider(bool use)
        {
            if (use)
            {
                _preBuildActions.Add(() =>
                {
                    _logger.CreateLogger("Init").LogCritical("Running with local in mem Service bus mock");
                    _pubSubProvider = new InMemoryMessageBus(_logger.CreateLogger<InMemoryMessageBus>(), _serviceProvider);
                });
            }
            return this;
        }

        public JobSchedulerBuilder<TJobPayload> UseAzureServicePubsubProvider(bool use)
        {
            if (use)
            {
                _preBuildActions.Add(() =>
                {
                    _pubSubProvider = new AzureServiceBusService(_config, _logger.CreateLogger<AzureServiceBusService>(), _serviceProvider);                    
                });
            }
            return this;
        }
    }
}