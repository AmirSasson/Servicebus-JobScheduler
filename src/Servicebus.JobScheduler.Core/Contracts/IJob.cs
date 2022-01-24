namespace Servicebus.JobScheduler.Core.Contracts.Messages
{
    public abstract class BaseJob : IJob
    {
        public string Id { get; set; }

        public string Etag { get; set; }
        public string JobType { get; set; }

    }

    public interface IJob
    {
        string Id { get; }
        string Etag { get; }
        string JobType { get; }

    }
}
