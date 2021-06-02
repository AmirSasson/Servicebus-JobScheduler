using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Extensions.Configuration;
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

namespace Servicebus.JobScheduler.Core.Bus
{
    public class AzureServiceBusService<TTopics, TSubscription> : IMessageBus<TTopics, TSubscription> where TTopics : struct, Enum where TSubscription : struct, Enum
    {
        private readonly ILogger _logger;
        private readonly string _runId;
        private readonly string _connectionString;
        private readonly ConcurrentDictionary<string, IClientEntity> _clientEntities = new();

        public AzureServiceBusService(IConfiguration config, ILogger logger, string runId)
        {
            _connectionString = config.GetValue<string>("ServiceBus:ConnectionString");
            if (string.IsNullOrEmpty(_connectionString))
            {
                throw new ArgumentNullException("ServiceBus:ConnectionString");
            }
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _runId = runId;

        }

        public async Task PublishAsync(IMessageBase msg, TTopics topic, DateTime? executeOnUtc = null)
        {
            var topicClient = _clientEntities.GetOrAdd(topic.ToString(), new TopicClient(connectionString: _connectionString, entityPath: topic.ToString())) as TopicClient;

            Message message = new()
            {
                MessageId = msg.Id,
                ContentType = "application/json",
                Body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(msg, msg.GetType())),
                PartitionKey = msg.Id
            };
            if (executeOnUtc.HasValue)
            {
                message.ScheduledEnqueueTimeUtc = executeOnUtc.Value;
            }
            try
            {
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

        public async Task<bool> RegisterSubscriber<T>(TTopics topic, TSubscription subscription, int concurrencyLevel, int simulateFailurePercents, IMessageHandler<T> handler, RetryPolicy<TTopics> deadLetterRetrying, CancellationTokenSource source)
            where T : class, IMessageBase
        {

            var subscriptionClient = _clientEntities.GetOrAdd(
                EntityNameHelper.FormatSubscriptionPath(topic.ToString(), subscription.ToString()),
                 new SubscriptionClient(
                    connectionString: _connectionString,
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
            var rand = new Random((int)DateTime.Now.Ticks);

            bool shouldSimulateError()
            {
                return rand.Next(0, 100) <= simulateFailurePercents;
            }

            _logger.LogInformation($"Registering {handler.GetType().Name} Subscriber to: {topic}:{subscription}");
            subscriptionClient.RegisterMessageHandler(
                async (msg, cancel) =>
                {
                    var str = Encoding.UTF8.GetString(msg.Body);
                    var obj = str.FromJson<T>();
                    if (obj.RunId != _runId)
                    {
                        // test igmore other runs
                        return;
                    }

                    _logger.LogInformation($"Incoming {topic}:{subscription}/{obj.Id} [{obj.GetType().Name}] delegate to {handler.GetType().Name}");

                    if (string.IsNullOrEmpty(msg.To) && shouldSimulateError())
                    {
                        throw new ApplicationException("Error simulation..");
                    }
                    await handler.Handle(obj);

                    _logger.LogInformation($"[{handler.GetType().Name}] - [{obj.Id}] retry {msg.SystemProperties.DeliveryCount} -receivedMsgCount: Handled success ");
                },
                new MessageHandlerOptions(args => { _logger.LogCritical(args.Exception, "Error"); return Task.CompletedTask; }) { AutoComplete = true, MaxConcurrentCalls = concurrencyLevel }
            );

            return true;
        }

        private async Task startDeadLetterRetryEngine(TTopics retriesTopic, TSubscription subscription, CancellationTokenSource source, RetryPolicy<TTopics> retry)
        {
            var dlpath = EntityNameHelper.FormatDeadLetterPath(EntityNameHelper.FormatSubscriptionPath(retriesTopic.ToString(), subscription.ToString()));

            var deadQueueReceiver = _clientEntities.GetOrAdd(dlpath.ToString(), new MessageReceiver(_connectionString, dlpath)) as MessageReceiver;

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Run(async () =>
            {
                var topicClient = _clientEntities.GetOrAdd(retriesTopic.ToString(), new TopicClient(connectionString: _connectionString, entityPath: retriesTopic.ToString())) as TopicClient;
                var permenantTopicClient = _clientEntities.GetOrAdd(retriesTopic.ToString(), new TopicClient(connectionString: _connectionString, entityPath: retry.PermanentErrorsTopic.ToString())) as TopicClient;

                while (!source.IsCancellationRequested)
                {
                    var msg = await deadQueueReceiver.ReceiveAsync();
                    if (msg != null && msg.MessageId.Contains(_runId))
                    {
                        var reSubmit = msg.Clone();
                        reSubmit.UserProperties.TryGetValue("retriesCount", out object retriesCountObj);
                        var retriesCount = Convert.ToInt32(retriesCountObj ?? 0);
                        reSubmit.To = subscription.ToString();
                        reSubmit.UserProperties["retriesCount"] = retriesCount + 1;
                        if (retriesCount < retry.RetryDefinition.MaxRetryCount)
                        {
                            var delay = retry.GetDelay(retriesCount);
                            reSubmit.ScheduledEnqueueTimeUtc = DateTime.UtcNow.Add(delay); //exponential backoff till max time
                            _logger.LogInformation($"Scheduling retry#{retriesCount + 1} to in {delay.TotalSeconds}sec due: {reSubmit.ScheduledEnqueueTimeUtc} Topic: {retriesTopic}, Subscription: {subscription},...");
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

        public async Task SetupEntitiesIfNotExist()
        {
            _logger.LogCritical("Running service bus Setup..");
            var adminClient = new ServiceBusAdministrationClient(_connectionString);

            var topicsNames = Enum.GetNames<TTopics>();

            foreach (var topicName in topicsNames)
            {
                bool topicExists = await adminClient.TopicExistsAsync(topicName);
                if (!topicExists)
                {
                    var options = new CreateTopicOptions(topicName)
                    {
                        MaxSizeInMegabytes = 1024,
                        //EnablePartitioning = true,
                    };
                    _logger.LogCritical($"creating missing topic {topicName}");
                    await adminClient.CreateTopicAsync(options);
                }
                else
                {
                    _logger.LogDebug($"topic exists {topicName}");
                }
            }

            var subscriptionNames = Enum.GetNames<TSubscription>();

            foreach (var subscriptionName in subscriptionNames)
            {
                var topicName = subscriptionName.Split("_").First();
                bool subscriptionExists = await adminClient.SubscriptionExistsAsync(topicName, subscriptionName);
                if (!subscriptionExists)
                {
                    var options = new CreateSubscriptionOptions(topicName, subscriptionName)
                    {
                        DefaultMessageTimeToLive = new TimeSpan(2, 0, 0, 0),
                        MaxDeliveryCount = 10,
                    };
                    _logger.LogCritical($"creating missing Subscription {topicName}:{subscriptionName}");
                    await adminClient.CreateSubscriptionAsync(options);

                    // rule - only specific to subscriptionName or to all
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
