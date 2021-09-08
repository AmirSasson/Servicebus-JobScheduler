using Microsoft.Extensions.Configuration;
using Servicebus.JobScheduler.Core.Contracts.Messages;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Servicebus.JobScheduler.Core.Contracts
{
    public interface IMessageBus : IAsyncDisposable
    {
        Task PublishAsync(BaseJob msg, string topic, DateTime? executeOnUtc = null);
        Task<bool> RegisterSubscriber<TMessage>(string topic, string subscription, int concurrencyLevel, IMessageHandler<TMessage> handler, RetryPolicy deadLetterRetrying, CancellationTokenSource source)
            where TMessage : class, IJob;
        Task SetupEntitiesIfNotExist(IConfiguration configuration, IEnumerable<string> topicsNames, IEnumerable<string> subscriptionNames);
    }
}