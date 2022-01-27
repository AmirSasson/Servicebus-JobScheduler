using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Servicebus.JobScheduler.Core.Contracts;
using Servicebus.JobScheduler.Core.Contracts.Messages;
using Servicebus.JobScheduler.Core.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Servicebus.JobScheduler.Core.Bus.Emulator
{
    public class InMemoryMessageBus : IMessageBus
    {
        class HandlerSubscription
        {
            public string Name { get; set; }
            public Func<object, JobExecutionContext, Task<HandlerResponse>> HandlingFunc { get; set; }
        }

        private const int MAX_RETRIES = 10;
        readonly Dictionary<string, IList<HandlerSubscription>> _eventHandlers = new();
        private readonly IServiceProvider _serviceProvider = null;

        private readonly ILogger _logger;

        public InMemoryMessageBus(ILogger logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task PublishAsync(BaseJob msg, string topic, DateTime? executeOnUtc = null, string corraltionId = null)
        {
            var scheduledEnqueueTimeUtcDescription = executeOnUtc.HasValue ? executeOnUtc.ToString() : "NOW";

            _logger.LogInformation($"Publishing to {topic} MessageId: {msg.Id} Time to Execute: {scheduledEnqueueTimeUtcDescription}");
            try
            {
                var testSerializationNotFailing = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(msg, msg.GetType()));
                var str = Encoding.UTF8.GetString(testSerializationNotFailing);
                var msgAfterSerializing = JsonSerializer.Deserialize(str, msg.GetType());
                if (string.IsNullOrWhiteSpace((msgAfterSerializing as BaseJob).Id))
                {
                    throw new ArgumentException("Message Id cannot be null or empty");
                }

                if (executeOnUtc.HasValue)
                {
                    int due = (int)(executeOnUtc.Value - DateTime.UtcNow).TotalMilliseconds;
                    var _ = new Timer((m) => { publishToSubscribers(m as BaseJob, topic); }, (msgAfterSerializing as BaseJob), Math.Max(due, 0), Timeout.Infinite);
                }
                else
                {
                    await publishToSubscribers((msgAfterSerializing as BaseJob), topic);
                }
            }
            catch (Exception e)
            {
                _logger.LogCritical(e, e.Message);
                throw;
            }
        }

        Task<bool> IMessageBus.RegisterSubscriber<TMessage>(string topic, string subscription, int concurrencyLevel, IMessageHandler<TMessage> handler, RetryPolicy deadLetterRetrying, CancellationTokenSource source)
         where TMessage : class
        {
            _logger.LogInformation($"Registering {handler.GetType().Name} Subscriber to: {topic}:{subscription}");

            if (!_eventHandlers.TryGetValue(subscription, out var subscribers))
            {
                subscribers = new List<HandlerSubscription>();
            }

            Func<object, JobExecutionContext, Task<HandlerResponse>> handlingFunction = async (object msg, JobExecutionContext ctx) =>
             {
                 var typedString = JsonSerializer.Serialize(msg);
                 var typed = JsonSerializer.Deserialize<TMessage>(typedString);
                 return await handler.Handle(typed, ctx);
             };

            subscribers.Add(new HandlerSubscription { Name = handler.GetType().Name, HandlingFunc = handlingFunction });

            _eventHandlers[subscription] = subscribers;
            return Task.FromResult(true);
        }

        Task<bool> IMessageBus.RegisterSubscriberType<TMessage, THandler>(string topic, string subscription, int concurrencyLevel, RetryPolicy deadLetterRetrying, CancellationTokenSource source)
        {
            _logger.LogInformation($"Registering {typeof(THandler).Name} Subscriber to: {topic}:{subscription}");

            if (!_eventHandlers.TryGetValue(subscription, out var subscribers))
            {
                subscribers = new List<HandlerSubscription>();

            }

            Func<object, JobExecutionContext, Task<HandlerResponse>> handlingFunction = async (object msg, JobExecutionContext ctx) =>
             {
                 using var handlerScope = _serviceProvider.CreateScope();

                 var handler = handlerScope.ServiceProvider.GetService<THandler>();
                 var typedString = JsonSerializer.Serialize(msg);
                 var typed = JsonSerializer.Deserialize<TMessage>(typedString);

                 return await handler.Handle(typed, ctx);
             };
            subscribers.Add(new HandlerSubscription { Name = typeof(THandler).Name, HandlingFunc = handlingFunction });
            _eventHandlers[subscription] = subscribers;
            return Task.FromResult(true);
        }

        public Task SetupEntitiesIfNotExist(IEnumerable<string> topicsNames, IEnumerable<string> subscriptionNames) => Task.CompletedTask;


        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        private async Task publishToSubscribers(BaseJob msg, string topic)
        {
            var correlationId = Guid.NewGuid();
            foreach (var eventHandlersKvp in _eventHandlers)
            {
                if (eventHandlersKvp.Key.ToString().Contains(topic.ToString(), StringComparison.InvariantCultureIgnoreCase))
                {
                    foreach (var handlerFunction in eventHandlersKvp.Value)
                    {
                        var handleSubscription = handlerFunction as HandlerSubscription;
                        var subscription = eventHandlersKvp.Key;
                        var handlerName = handleSubscription.Name;

                        _logger.LogInformation($"Incoming {topic}:{subscription}/{msg.Id} [{msg.GetType().Name}] delegate to {handlerName}");

                        var retries = 0;
                        var success = false;
                        while (!success && retries < MAX_RETRIES)
                        {
                            try
                            {
                                var result = await handleSubscription.HandlingFunc(msg, new JobExecutionContext
                                {
                                    RetryBatchesCount = retries,
                                    IsLastRetry = retries == MAX_RETRIES - 1,
                                    RetriesInCurrentBatch = retries,
                                    MaxRetriesInBatch = MAX_RETRIES,
                                    MaxRetryBatches = 0,
                                    RetryPolicy = null,
                                    MsgCorrelationId = correlationId.ToString()
                                });
                                success = true;
                                _logger.LogInformation($"[{handlerName}] - [{msg.Id}] - [{topic}] Success, result : {result.ToJson()} ");

                                if (result.ContinueWithResult != null)
                                {
                                    await PublishAsync(result.ContinueWithResult.Message, result.ContinueWithResult.TopicToPublish, result.ContinueWithResult.ExecuteOnUtc);
                                }
                                else
                                {
                                    _logger.LogWarning($"[{handlerName}] - [{msg.Id}] - [{topic}] Got to its final stage!! ");

                                }
                            }
                            catch (ExecutionPermanentException e)
                            {
                                _logger.LogError($"{handlerName} Failed to handle {topic}:{subscription}/{msg.Id} [{msg.GetType().Name}] with  {e.GetType().Name} Error! {e}");
                                return;
                            }
                            catch (System.Exception e)
                            {
                                retries++;
                                _logger.LogError($"[{handlerName}] - [{msg.Id}] - [{topic}] Handler Failed to handle msg on retry [{retries}] error: {e.Message} ");
                            }
                        }
                    };
                }
            }
        }
    }
}
