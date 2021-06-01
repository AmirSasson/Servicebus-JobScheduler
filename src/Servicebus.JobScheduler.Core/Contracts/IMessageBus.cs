using Servicebus.JobScheduler.Core.Bus;
using Servicebus.JobScheduler.Core.Contracts.Messages;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Servicebus.JobScheduler.Core.Contracts
{
    public interface IMessageBus<TTopics, TSubscription> where TTopics : struct, Enum where TSubscription : struct, Enum
    {
        Task PublishAsync(IMessageBase msg, TTopics topic, DateTime? executeOnUtc = null);
        Task<bool> RegisterSubscriber<T>(TTopics topic, TSubscription subscription, int concurrencyLevel, int simulateFailurePercents, IMessageHandler<T> handler, RetryPolicy<TTopics> deadLetterRetrying, CancellationTokenSource source)
            where T : class, IMessageBase;
        Task SetupEntitiesIfNotExist();
    }
}