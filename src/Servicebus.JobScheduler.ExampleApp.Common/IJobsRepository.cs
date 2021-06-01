using Servicebus.JobScheduler.ExampleApp.Messages;
using System.Threading.Tasks;

namespace Servicebus.JobScheduler.ExampleApp.Common
{
    public interface IRepository<T>
    {
        Task<T> GetById(string jobDefinationId);
        Task<T> Upsert(T jobDefination);
    }
}