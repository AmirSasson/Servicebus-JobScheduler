using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Extensions.Logging;
using Servicebus.JobScheduler.Core.Contracts;
using Servicebus.JobScheduler.Core.Contracts.Messages;
using Servicebus.JobScheduler.Core.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Servicebus.JobScheduler.Core.Bus
{
    public class AzureServiceBusService : IMessageBus
    {
        private readonly ILogger _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IOptions<ServiceBusConfig> _config;
        private readonly ConcurrentDictionary<string, IClientEntity> _clientEntities = new ConcurrentDictionary<string, IClientEntity>();

        public AzureServiceBusService(IOptions<ServiceBusConfig> config, ILogger logger, IServiceProvider serviceProvider)
        {
            _config = config;
            if (string.IsNullOrEmpty(config.Value.ConnectionString))
            {
                throw new ArgumentNullException("ServiceBus:ConnectionString");
            }
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _serviceProvider = serviceProvider;
        }

        public async Task PublishAsync(BaseJob msg, string topic, DateTime? executeOnUtc = null, string correlationId = null)
        {
            var topicClient = _clientEntities.GetOrAdd(topic.ToString(), (path) => new TopicClient(connectionString: _config.Value.ConnectionString, entityPath: path)) as TopicClient;
            try
            {
                Message message = new Message()
                {
                    MessageId = msg.Id,
                    ContentType = "application/json",
                    Body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(msg, msg.GetType())),
                    PartitionKey = msg.Id,
                    CorrelationId = correlationId ?? Guid.NewGuid().ToString()
                };
                if (executeOnUtc.HasValue)
                {
                    message.ScheduledEnqueueTimeUtc = executeOnUtc.Value;
                }

                var scheduledEnqueueTimeUtcDescription = executeOnUtc.HasValue ? executeOnUtc.ToString() : "NOW";
                _logger.LogInformation($"Publishing to {topic} MessageId: {msg.Id} Time to Execute: {scheduledEnqueueTimeUtcDescription}");
                await topicClient.SendAsync(message);
            }
            catch (Exception e)
            {
                _logger.LogCritical(e, e.Message);
                throw;
            }
        }

        public async Task<bool> RegisterSubscriber<TMessage>(string topic, string subscription, int concurrencyLevel, IMessageHandler<TMessage> handler, Contracts.RetryPolicy deadLetterRetrying, CancellationTokenSource source)
            where TMessage : class, IJob
        {
            Func<TMessage, JobExecutionContext, Task<HandlerResponse>> handlingFunction = async (TMessage msg, JobExecutionContext ctx) =>
             {
                 return await handler.Handle(msg, ctx);
             };
            return await registerSubscriberPrivate(topic, subscription, handler.GetType().Name, handlingFunction, concurrencyLevel, deadLetterRetrying, source);
        }


        public async Task<bool> RegisterSubscriberType<TMessage, THandler>(string topic, string subscription, int concurrencyLevel, Contracts.RetryPolicy deadLetterRetrying, CancellationTokenSource source)
            where TMessage : class, IJob
            where THandler : IMessageHandler<TMessage>
        {

            Func<TMessage, JobExecutionContext, Task<HandlerResponse>> handlingFunction = async (TMessage msg, JobExecutionContext ctx) =>
            {
                using var handlerScope = _serviceProvider.CreateScope();

                var handler = handlerScope.ServiceProvider.GetService<THandler>();

                return await handler.Handle(msg, ctx);
            };
            return await registerSubscriberPrivate(topic, subscription, typeof(THandler).Name, handlingFunction, concurrencyLevel, deadLetterRetrying, source);
        }


        private async Task<bool> registerSubscriberPrivate<TMessage>(string topic, string subscription, string handlerName, Func<TMessage, JobExecutionContext, Task<HandlerResponse>> handlingFunction, int concurrencyLevel, Contracts.RetryPolicy deadLetterRetrying, CancellationTokenSource source)
                 where TMessage : class, IJob
        {
            var subscriptionClient = _clientEntities.GetOrAdd(
                EntityNameHelper.FormatSubscriptionPath(topic.ToString(), subscription.ToString()),
                (_) =>
                 new SubscriptionClient(
                    connectionString: _config.Value.ConnectionString,
                    topicPath: topic.ToString(),
                    subscriptionName: subscription.ToString(),
                    ReceiveMode.PeekLock,
                    retryPolicy: new Microsoft.Azure.ServiceBus.RetryExponential(TimeSpan.FromSeconds(10), TimeSpan.FromHours(3), 20)
                    )
                )
                as SubscriptionClient;

            if (deadLetterRetrying != null)
            {
                await startDeadLetterRetryEngine(topic, subscription, source, deadLetterRetrying);//permanentErrorsTopic.Value, new RetryExponential(TimeSpan.FromSeconds(40), TimeSpan.FromMinutes(2), 3));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            }
            _logger.LogInformation($"Registering {handlerName} Subscriber to: {topic}:{subscription}");
            var maxImmediateRetriesInBatch = _config.Value.GetSubscriberConfig(subscription).MaxImmediateRetriesInBatch;
            subscriptionClient.RegisterMessageHandler(
                async (msg, cancel) =>
                {
                    var str = Encoding.UTF8.GetString(msg.Body);
                    var obj = str.FromJson<TMessage>();


                    msg.UserProperties.TryGetValue("retriesCount", out object retriesCountObj);
                    var retryBatchesCount = Convert.ToInt32(retriesCountObj ?? 0);

                    using var ls = _logger.BeginScope("[{CorrelationId}]", msg.CorrelationId ?? $"{topic}:{subscription}/{obj.Id}");
                    _logger.LogInformation($"Incoming {topic}:{subscription}/{obj.Id} [{obj.GetType().Name}] delegate to {handlerName}");
                    try
                    {
                        var maxRetriesInBatch = maxImmediateRetriesInBatch;
                        var maxRetryBatches = deadLetterRetrying?.RetryDefinition?.MaxRetryCount ?? 0;

                        var execContext = new JobExecutionContext
                        {
                            MaxRetryBatches = maxRetryBatches,
                            MaxRetriesInBatch = maxRetriesInBatch,
                            RetryBatchesCount = retryBatchesCount,
                            RetryPolicy = deadLetterRetrying,
                            IsLastRetry = retryBatchesCount >= maxRetryBatches && msg.SystemProperties.DeliveryCount >= maxRetriesInBatch,
                            MsgCorrelationId = msg.CorrelationId,
                            RetriesInCurrentBatch = msg.SystemProperties.DeliveryCount
                        };

                        var handlerResponse = await handlingFunction(obj, execContext);

                        if (handlerResponse.ContinueWithResult != null)
                        {
                            await PublishAsync(handlerResponse.ContinueWithResult.Message, handlerResponse.ContinueWithResult.TopicToPublish, handlerResponse.ContinueWithResult.ExecuteOnUtc, msg.CorrelationId);
                        }

                        _logger.LogInformation($"[{handlerName}] - [{obj.Id}] retry {msg.SystemProperties.DeliveryCount} -receivedMsgCount: Handled success ");
                    }
                    catch (ExecutionPermanentException e)
                    {
                        _logger.LogError($"{handlerName} Failed to handle {topic}:{subscription}/{obj.Id} [{obj.GetType().Name}] with  {e.GetType().Name} Error! {e}");
                        if (!string.IsNullOrEmpty(deadLetterRetrying?.PermanentErrorsTopic))
                        {
                            await PublishAsync(obj as BaseJob, deadLetterRetrying?.PermanentErrorsTopic, executeOnUtc: null, msg.CorrelationId);
                        }
                        return;
                    }
                },
                new MessageHandlerOptions(
                    args =>
                    {
                        _logger.LogCritical(args.Exception, "Error");
                        return Task.CompletedTask;
                    })
                {
                    AutoComplete = true,
                    MaxConcurrentCalls = concurrencyLevel
                }
            );

            return true;
        }

        private async Task startDeadLetterRetryEngine(string retriesTopic, string subscription, CancellationTokenSource source, Contracts.RetryPolicy retry)
        {
            var dlqPath = EntityNameHelper.FormatDeadLetterPath(EntityNameHelper.FormatSubscriptionPath(retriesTopic.ToString(), subscription.ToString()));

            var deadQueueReceiver = _clientEntities.GetOrAdd(dlqPath.ToString(), (path) => new MessageReceiver(_config.Value.ConnectionString, path)) as MessageReceiver;

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Run(async () =>
            {
                var topicClient = _clientEntities.GetOrAdd(retriesTopic.ToString(), (path) => new TopicClient(connectionString: _config.Value.ConnectionString, entityPath: path)) as TopicClient;
                var permenantTopicClient = _clientEntities.GetOrAdd(retry.PermanentErrorsTopic.ToString(), (path) => new TopicClient(connectionString: _config.Value.ConnectionString, entityPath: path)) as TopicClient;

                while (!source.IsCancellationRequested)
                {
                    var msg = await deadQueueReceiver.ReceiveAsync();
                    if (msg != null)
                    {
                        var reSubmit = msg.Clone();
                        reSubmit.UserProperties.TryGetValue("retriesCount", out object retriesCountObj);
                        var retriesCount = Convert.ToInt32(retriesCountObj ?? 0);
                        reSubmit.To = subscription.ToString();
                        reSubmit.UserProperties["retriesCount"] = retriesCount + 1;
                        reSubmit.UserProperties.TryGetValue("DeadLetterReason", out object dlqReason);
                        if (retriesCount < retry.RetryDefinition.MaxRetryCount)
                        {
                            var delay = retry.GetDelay(retriesCount);
                            reSubmit.ScheduledEnqueueTimeUtc = DateTime.UtcNow.Add(delay); //exponential backoff till max time
                            _logger.LogInformation($"Scheduling retry#{retriesCount + 1} to in {delay.TotalSeconds}sec due: {reSubmit.ScheduledEnqueueTimeUtc} Topic: {retriesTopic}, Subscription: {subscription} reason:{dlqReason},...");
                            try
                            {

                                await topicClient.SendAsync(reSubmit);
                                await deadQueueReceiver.CompleteAsync(msg.SystemProperties.LockToken);
                            }
                            catch (Exception e)
                            {
                                throw;
                            }
                        }
                        else
                        {
                            _logger.LogCritical($"Reries were exhusted !! moving to {retry.PermanentErrorsTopic}");
                            await permenantTopicClient.SendAsync(reSubmit);
                            await deadQueueReceiver.CompleteAsync(msg.SystemProperties.LockToken);
                        }
                    }
                }
            });
        }

        public async Task SetupEntitiesIfNotExist(IEnumerable<string> topicsNames, IEnumerable<string> subscriptionNames)
        {
            _logger.LogCritical("Running service bus Setup..");
            var adminClient = new ServiceBusAdministrationClient(_config.Value.ConnectionString);

            CreateTopicOptions getTopicOptions(string topicName)
            {
                var maxSizeInMegabytes = _config.Value.GetTopicConfig(topicName).MaxSizeInMegabytes;
                var enablePartitioning = _config.Value.GetTopicConfig(topicName).EnablePartitioning;
                return new CreateTopicOptions(topicName) { MaxSizeInMegabytes = maxSizeInMegabytes, EnablePartitioning = enablePartitioning };
            }

            foreach (var topicName in topicsNames)
            {
                bool topicExists = await adminClient.TopicExistsAsync(topicName);
                if (!topicExists)
                {
                    var options = getTopicOptions(topicName);
                    _logger.LogCritical($"creating missing topic {topicName}");
                    await adminClient.CreateTopicAsync(options);
                }
                else
                {
                    _logger.LogDebug($"topic exists {topicName}");
                }
            }

            CreateSubscriptionOptions getSubscriptionOptions(string topicName, string subscriptionName)
            {
                var maxImmediateRetriesInBatch = _config.Value.GetSubscriberConfig(subscriptionName).MaxImmediateRetriesInBatch;

                return new CreateSubscriptionOptions(topicName, subscriptionName)
                {
                    MaxDeliveryCount = maxImmediateRetriesInBatch,
                    DefaultMessageTimeToLive = new TimeSpan(2, 0, 0, 0),
                };
            }
            foreach (var subscriptionName in subscriptionNames)
            {
                var topicName = subscriptionName.Split("_").First(); // subscription name should be in the format : <<TOPIC NAME>>_<<SUBSCRIPTION NAME>>
                if (!topicsNames.Contains(topicName))
                {
                    throw new InvalidOperationException($"Subscription Name {subscriptionName} is invalid, must be in the format:  <<TOPIC NAME>>_<<SUBSCRIPTION NAME>>, no matching topic name was found!");
                }
                bool subscriptionExists = await adminClient.SubscriptionExistsAsync(topicName, subscriptionName);
                if (!subscriptionExists)
                {
                    var options = getSubscriptionOptions(topicName, subscriptionName);
                    _logger.LogCritical($"creating missing Subscription {topicName}:{subscriptionName}");
                    await adminClient.CreateSubscriptionAsync(options);

                    // rule - only specific to this subscription or to all
                    var rule = await adminClient.GetRuleAsync(topicName, subscriptionName, RuleProperties.DefaultRuleName);
                    rule.Value.Filter = new SqlRuleFilter($"sys.To IS NULL OR sys.To = '{subscriptionName}'");
                    await adminClient.UpdateRuleAsync(topicName, subscriptionName, rule.Value);
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            _logger.LogCritical($"Gracefully disposing Engine!");
            IEnumerable<Task> unRegisterTasks = _clientEntities.Values
                .Where(c => c is SubscriptionClient)
                .Cast<SubscriptionClient>()
                .Select(c =>
                {
                    _logger.LogCritical($"Stopping listener.. {c.Path}...");
                    var t = c.UnregisterMessageHandlerAsync(TimeSpan.FromSeconds(0.5));
                    t.ConfigureAwait(false);
                    return t;
                });

            await Task.WhenAll(unRegisterTasks.ToArray()).ConfigureAwait(false);
            await Task.Delay(3500);
            _logger.LogCritical($"Closing connections!");

            var closingTasks = _clientEntities.Values.Select(c =>
            {
                _logger.LogCritical($"Closing client {c.Path}...");
                var t = c.CloseAsync();
                t.ConfigureAwait(false);
                return t;
            });
            await Task.WhenAll(closingTasks.ToArray()).ConfigureAwait(false);
            _logger.LogCritical($"All connections gracefully closed");
        }
    }
}
