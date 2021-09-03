namespace Servicebus.JobScheduler.Core.Contracts.Messages
{
    public class BaseMessage : IBaseMessage
    {
        public string Id { get; }
        public string RunId { get; }
    }

    public interface IBaseMessage
    {
        string Id { get; }
        string RunId { get; }
    }
}
