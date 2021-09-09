using System;

namespace Servicebus.JobScheduler.Core.Contracts
{
    public class RetryPolicy
    {
        public string PermanentErrorsTopic { get; set; }
        public IRetryDefination RetryDefinition { get; set; }

        public TimeSpan GetDelay(int retriesCount)
        {
            return RetryDefinition.GetDelay(retriesCount);
        }
    }

    public sealed class RetryExponential : IRetryDefination
    {
        public RetryExponential(TimeSpan minimalBackoff, TimeSpan maximalBackoff, int maxRetries)
        {
            Minimal = minimalBackoff;
            Maximal = maximalBackoff;
            MaxRetryCount = maxRetries;

        }
        public TimeSpan Minimal { get; private set; }
        public TimeSpan Maximal { get; private set; }
        public int MaxRetryCount { get; private set; }

        public TimeSpan GetDelay(int retriesCount)
        {
            return TimeSpan.FromSeconds(Math.Min(Minimal.TotalSeconds * Math.Pow(2, retriesCount), Maximal.TotalSeconds));
        }
    }

    public interface IRetryDefination
    {
        TimeSpan Minimal { get; }
        TimeSpan Maximal { get; }
        int MaxRetryCount { get; }
        TimeSpan GetDelay(int currentRetry);
    }

}