using Servicebus.JobScheduler.Core.Contracts.Messages;
using System;
using System.Threading.Tasks;

namespace Servicebus.JobScheduler.Core.Contracts
{
    public interface IMessageHandler<TTopics, TMessageType> where TTopics : struct, Enum where TMessageType : class, IBaseMessage
    {
        Task<HandlerResponse<TTopics>> Handle(TMessageType msg);
    }
}
