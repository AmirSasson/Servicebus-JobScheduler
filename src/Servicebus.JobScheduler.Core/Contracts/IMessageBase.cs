namespace Servicebus.JobScheduler.Core.Contracts.Messages
{
    public interface IMessageBase
    {
        public string Id { get; }
        public string RunId { get; }
    }
}
