using Microsoft.Extensions.Configuration;
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
namespace Servicebus.JobScheduler.ExampleApp.Emulators
{
    public class InMemoryMessageBus : IMessageBus
    {
        class DummyMessage : BaseJob
        {
        }

        enum DummyTopic
        {
        }
        readonly Dictionary<string, IList> _eventHandlers = new();
        private readonly ILogger _logger;

        public InMemoryMessageBus(ILogger logger)
        {
            _logger = logger;
        }

        public async Task PublishAsync(BaseJob msg, string topic, DateTime? executeOnUtc = null)
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
                    var _ = new Timer((m) => { publishToSubscribers(m as BaseJob, topic); }, msg, Math.Max(due, 0), Timeout.Infinite);
                }
                else
                {
                    await publishToSubscribers(msg, topic);
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

            if (!_eventHandlers.TryGetValue(subscription, out var subs))
            {
                subs = new List<IMessageHandler<TMessage>>();
            }
            subs.Add(handler);
            _eventHandlers[subscription] = subs;
            return Task.FromResult(true);
        }

        public Task SetupEntitiesIfNotExist(IConfiguration _, IEnumerable<string> topicsNames, IEnumerable<string> subscriptionNames) => Task.CompletedTask;


        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        private async Task publishToSubscribers(BaseJob msg, string topic)
        {
            foreach (var eventHandlersKvp in _eventHandlers)
            {
                if (eventHandlersKvp.Key.ToString().Contains(topic.ToString(), StringComparison.InvariantCultureIgnoreCase))
                {
                    foreach (var h in eventHandlersKvp.Value)
                    {
                        _logger.LogInformation($"Incoming {topic}:{eventHandlersKvp.Key}/{msg.Id} [{msg.GetType().Name}] delegate to {h.GetType().Name}");

                        var handleMethod = h.GetType().GetMethod(nameof(IMessageHandler<DummyMessage>.Handle));

                        var retries = 0;
                        var success = false;
                        while (!success && retries < 10)
                        {
                            try
                            {
                                var task = handleMethod.Invoke(h, new[] { msg });
                                var handlerResult = (task as Task<HandlerResponse>);
                                var result = await handlerResult;
                                success = true;
                                _logger.LogInformation($"[{h.GetType().Name}] - [{msg.Id}] - [{topic}] Success, result : {result.ToJson()} ");

                                if (result.ContinueWithResult != null)
                                {
                                    // dont wait
                                    await PublishAsync(result.ContinueWithResult.Message, result.ContinueWithResult.TopicToPublish, result.ContinueWithResult.ExecuteOnUtc);
                                }
                                else
                                {
                                    _logger.LogWarning($"[{h.GetType().Name}] - [{msg.Id}] - [{topic}] Got to its final stage!! ");

                                }
                            }
                            catch (System.Exception e)
                            {
                                retries++;
                                _logger.LogError($"[{h.GetType().Name}] - [{msg.Id}] - [{topic}] Handler Failed to handle msg on retry [{retries}] error: {e.Message} ");
                            }
                        }
                    };
                }
            }
        }
    }
}
