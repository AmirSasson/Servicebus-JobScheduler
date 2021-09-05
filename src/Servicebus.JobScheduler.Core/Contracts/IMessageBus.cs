using Microsoft.Extensions.Configuration;
using Servicebus.JobScheduler.Core.Contracts.Messages;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Servicebus.JobScheduler.Core.Contracts
{
    public interface IMessageBus<TTopics, TSubscription> : IAsyncDisposable where TTopics : struct, Enum where TSubscription : struct, Enum
    {
        Task PublishAsync(BaseMessage msg, TTopics topic, DateTime? executeOnUtc = null);
        Task<bool> RegisterSubscriber<TMessage>(TTopics topic, TSubscription subscription, int concurrencyLevel, IMessageHandler<TTopics, TMessage> handler, RetryPolicy<TTopics> deadLetterRetrying, CancellationTokenSource source)
            where TMessage : class, IBaseMessage;
        Task SetupEntitiesIfNotExist(IConfiguration config);
    }
}