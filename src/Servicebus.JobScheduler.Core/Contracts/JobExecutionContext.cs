namespace Servicebus.JobScheduler.Core.Contracts
{
    public class JobExecutionContext
    {
        public int RetryBatchesCount { get; internal set; }
        public RetryPolicy RetryPolicy { get; internal set; }
        public bool IsLastRetry { get; internal set; }
        public string MsgCorrelationId { get; internal set; }
        public int RetriesInCurrentBatch { get; internal set; }
        public int MaxRetryBatches { get; internal set; }
        public int MaxRetriesInBatch { get; internal set; }
    }
}