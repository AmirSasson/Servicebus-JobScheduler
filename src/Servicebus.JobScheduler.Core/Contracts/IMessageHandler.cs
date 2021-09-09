using Servicebus.JobScheduler.Core.Contracts.Messages;
using System;
using System.Threading.Tasks;

namespace Servicebus.JobScheduler.Core.Contracts
{
    public interface IMessageHandler<TMessageType> where TMessageType : class, IJob
    {
        Task<HandlerResponse> Handle(TMessageType msg);
    }
}
