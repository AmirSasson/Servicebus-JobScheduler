using Microsoft.Extensions.Logging;
using Servicebus.JobScheduler.Core.Contracts;
using Servicebus.JobScheduler.Core.Contracts.Messages;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Servicebus.JobScheduler.ExampleApp.Emulators
{
    public class InMemoryMessageBus<TTopics, TSubscription> : IMessageBus<TTopics, TSubscription> where TTopics : struct, Enum where TSubscription : struct, Enum
    {
        class DummyMessage : IMessageBase
        {
            public string Id => "1";

            public string RunId => "1";
        }
        readonly Dictionary<TSubscription, IList> _eventHandlers = new();
        private readonly ILogger _logger;

        public InMemoryMessageBus(ILogger logger)
        {
            _logger = logger;
        }

        public Task PublishAsync(IMessageBase msg, TTopics topic, DateTime? executeOnUtc = null)
        {
            var scheduledEnqueueTimeUtcDescription = executeOnUtc.HasValue ? executeOnUtc.ToString() : "NOW";

            _logger.LogInformation($"Publishing to {topic} MessageId: {msg.Id} Time to Execute: {scheduledEnqueueTimeUtcDescription}");

            if (executeOnUtc.HasValue)
            {
                int due = (int)(executeOnUtc.Value - DateTime.UtcNow).TotalMilliseconds;
                var _ = new Timer((m) => { publishToSubscribers(m as IMessageBase, topic); }, msg, Math.Max(due, 0), Timeout.Infinite);
            }
            else
            {
                publishToSubscribers(msg, topic);
            }
            return Task.CompletedTask;
        }

        private void publishToSubscribers(IMessageBase msg, TTopics topic)
        {
            foreach (var eventHandlersKvp in _eventHandlers)
            {
                if (eventHandlersKvp.Key.ToString().Contains(topic.ToString(), StringComparison.InvariantCultureIgnoreCase))
                {
                    foreach (var h in eventHandlersKvp.Value)
                    {
                        _logger.LogInformation($"Incoming {topic}:{eventHandlersKvp.Key}/{msg.Id} [{msg.GetType().Name}] delegate to {h.GetType().Name}");

                        var handleMethod = h.GetType().GetMethod(nameof(IMessageHandler<DummyMessage>.Handle));
                        var task = handleMethod.Invoke(h, new[] { msg });

                        (task as Task)?.ContinueWith((res) =>
                        {
                            _logger.LogInformation($"[{h.GetType().Name}] - [{msg.Id}] receivedMsgCount: Handled success ");

                        });
                    };
                }
            }
        }

        public Task SetupEntitiesIfNotExist() => Task.CompletedTask;

        public Task<bool> RegisterSubscriber<T>(TTopics topic, TSubscription subscription, int concurrencyLevel, IMessageHandler<T> handler, RetryPolicy<TTopics> deadLetterRetrying, CancellationTokenSource source)
         where T : class, IMessageBase
        {
            _logger.LogInformation($"Registering {handler.GetType().Name} Subscriber to: {topic}:{subscription}");

            if (!_eventHandlers.TryGetValue(subscription, out var subs))
            {
                subs = new List<IMessageHandler<T>>();
            }
            subs.Add(handler);
            _eventHandlers[subscription] = subs;
            return Task.FromResult(true);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
