using Servicebus.JobScheduler.Core.Contracts.Messages;
using System.Threading.Tasks;

namespace Servicebus.JobScheduler.Core.Contracts
{
    public interface IMessageHandler<T> where T : class, IMessageBase
    {
        Task<bool> Handle(T msg);
    }
}
