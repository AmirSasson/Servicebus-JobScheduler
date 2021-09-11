

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Servicebus.JobScheduler.Core.Bus;
using Servicebus.JobScheduler.Core.Bus.Emulator;
using Servicebus.JobScheduler.Core.Contracts;
using Servicebus.JobScheduler.Core.Contracts.Messages;

namespace Servicebus.JobScheduler.Core
{
    public class JobSchedulerBuilder<TJobPayload>
    {
        private Contracts.IMessageBus _pubSubProvider;
        private ILoggerFactory _logger = new Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory();
        private IConfiguration _config;
        private bool _initiateSchedulingWorkers = true;
        private CancellationTokenSource _source;
        private IJobChangeProvider _changeProvider = new EmptyChangeProvider();
        private List<Action> _preBuildActions = new();
        private List<Func<JobScheduler<TJobPayload>, Task>> _buildTasks = new();
        private IServiceProvider _serviceProvider;
        private readonly HashSet<string> _topics = Enum.GetNames<SchedulingTopics>().ToHashSet();
        private readonly HashSet<string> _subscriptions = Enum.GetNames<SchedulingSubscriptions>().ToHashSet();

        public JobSchedulerBuilder<TJobPayload> UsePubsubProvider(Contracts.IMessageBus pubSubProvider)
        {
            _pubSubProvider = pubSubProvider;
            return this;
        }
        public JobSchedulerBuilder<TJobPayload> UseLoggerFactory(ILoggerFactory logger)
        {
            _logger = logger;
            return this;
        }
        public JobSchedulerBuilder<TJobPayload> UseSchedulingWorker(bool initiateSchedulingWorkers = true)
        {
            _initiateSchedulingWorkers = initiateSchedulingWorkers;
            return this;
        }

        public JobSchedulerBuilder<TJobPayload> WithCancelationSource(CancellationTokenSource source)
        {
            _source = source;
            return this;
        }

        public JobSchedulerBuilder<TJobPayload> WithConfiguration(IConfiguration config)
        {
            _config = config;
            return this;
        }

        public JobSchedulerBuilder<TJobPayload> UseJobChangeProvider(IJobChangeProvider changeProvider)
        {
            _changeProvider = changeProvider;
            return this;
        }

        public JobSchedulerBuilder<TJobPayload> WithHandlersServiceProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            return this;
        }

        public IJobScheduler<TJobPayload> Build()
        {
            return BuildAsync().Result;
        }
        public async Task<IJobScheduler<TJobPayload>> BuildAsync()
        {
            foreach (var action in _preBuildActions)
            {
                action();
            }

            _pubSubProvider = _pubSubProvider ?? new AzureServiceBusService(_config, _logger.CreateLogger<AzureServiceBusService>(), _serviceProvider);
            var scheduler = new JobScheduler<TJobPayload>(_pubSubProvider, _logger.CreateLogger<JobScheduler<TJobPayload>>());

            await scheduler.SetupEntities(_config, _topics, _subscriptions);

            if (_initiateSchedulingWorkers)
            {
                await scheduler.StartSchedulingWorkers(_config, _logger, _changeProvider, _source);
            }
            foreach (var task in _buildTasks)
            {
                await task(scheduler);
            }

            return scheduler;
        }

        public JobSchedulerBuilder<TJobPayload> AddMainJobExecuter(IMessageHandler<JobWindow<TJobPayload>> mainHandler, int concurrencyLevel, RetryPolicy retryPolicy, bool enabled = true)
        {
            if (retryPolicy != null && !string.IsNullOrWhiteSpace(retryPolicy.PermanentErrorsTopic))
            {
                _topics.Add(retryPolicy.PermanentErrorsTopic);
            }
            if (enabled)
            {
                _buildTasks.Add(async (_) =>
                {
                    await _pubSubProvider.RegisterSubscriber(
                        SchedulingTopics.JobWindowValid.ToString(),
                        SchedulingSubscriptions.JobWindowValid_RuleTimeWindowExecution.ToString(),
                        concurrencyLevel: 3,
                        mainHandler,
                        retryPolicy,
                        _source);
                });
            }
            return this;
        }

        public JobSchedulerBuilder<TJobPayload> AddMainJobExecuterType<THandler>(int concurrencyLevel, RetryPolicy retryPolicy, bool enabled = true)
            where THandler : IMessageHandler<JobWindow<TJobPayload>>
        {
            if (retryPolicy != null && !string.IsNullOrWhiteSpace(retryPolicy.PermanentErrorsTopic))
            {
                _topics.Add(retryPolicy.PermanentErrorsTopic);
            }
            if (enabled)
            {
                _buildTasks.Add(async (_) =>
                {
                    await _pubSubProvider.RegisterSubscriberType<JobWindow<TJobPayload>, THandler>(
                        SchedulingTopics.JobWindowValid.ToString(),
                        SchedulingSubscriptions.JobWindowValid_RuleTimeWindowExecution.ToString(),
                        concurrencyLevel: 3,
                        retryPolicy,
                        _source);
                });
            }
            return this;
        }

        public JobSchedulerBuilder<TJobPayload> AddSubJobHandler<TMessageType>(string topic, string sub, IMessageHandler<TMessageType> handler, int concurrencyLevel, RetryPolicy retryPolicy = null, bool enabled = true) where TMessageType : class, IJob
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

        public JobSchedulerBuilder<TJobPayload> AddSubJobHandlerType<TMessageType, THandler>(string topic, string sub, int concurrencyLevel, RetryPolicy retryPolicy = null, bool enabled = true)
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

        public JobSchedulerBuilder<TJobPayload> UseInMemoryPubsubProvider(bool use)
        {
            if (use)
            {
                _preBuildActions.Add(() =>
                {
                    _logger.CreateLogger("Init").LogCritical("Running with local in mem Service bus mock");
                    _pubSubProvider = new InMemoryMessageBus(_logger.CreateLogger<InMemoryMessageBus>());
                });
            }
            return this;
        }
    }
}