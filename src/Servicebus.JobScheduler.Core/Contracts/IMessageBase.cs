namespace Servicebus.JobScheduler.Core.Contracts.Messages
{
    public class BaseMessage : IBaseMessage
    {
        public string Id { get; set; }
        public string RunId { get; set; }
    }

    public interface IBaseMessage
    {
        string Id { get; }
        string RunId { get; }
    }
}
