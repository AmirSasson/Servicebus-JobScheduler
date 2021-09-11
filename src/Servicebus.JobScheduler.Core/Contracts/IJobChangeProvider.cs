using System.Threading.Tasks;

namespace Servicebus.JobScheduler.Core.Contracts.Messages
{
    public interface IJobChangeProvider
    {
        Task<ChangeType> GetJobChangeType(string jobId, string etag);
    }
    public enum ChangeType
    {
        NotChanged,
        Deleted,
        Changed
    }
    public class EmptyChangeProvider : IJobChangeProvider
    {
        public Task<ChangeType> GetJobChangeType(string jobId, string etag) => Task.FromResult(ChangeType.NotChanged);
    }
}
