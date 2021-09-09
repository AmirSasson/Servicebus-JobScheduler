namespace Servicebus.JobScheduler.Core.Contracts.Messages
{
    public abstract class BaseJob : IJob
    {
        public string Id { get; set; }

        public string Etag { get; set; }
    }

    public interface IJob
    {
        string Id { get; }
        string Etag { get; }

    }
}
