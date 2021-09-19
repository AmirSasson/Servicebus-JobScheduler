using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace Servicebus.JobScheduler.Core.Contracts
{
    public class InMemOptions<TOptions> : IOptions<TOptions> where TOptions : class
    {
        public InMemOptions(TOptions options)
        {
            this.Value = options;
        }
        public TOptions Value { get; private set; }
    }

    public class ServiceBusConfig
    {
        public string ConnectionString { get; set; }
        public Dictionary<string, TopicConfig> TopicsConfig { get; set; }
        public Dictionary<string, SubscriberConfig> SubscribersConfig { get; set; }

        public TopicConfig GetTopicConfig(string topicName)
        {
            if (this.TopicsConfig.TryGetValue(topicName, out var tconfig))
            {
                return tconfig;
            }
            return TopicConfig.Default;
        }
        public SubscriberConfig GetSubscriberConfig(string subName)
        {
            if (this.SubscribersConfig.TryGetValue(subName, out var tconfig))
            {
                return tconfig;
            }
            return SubscriberConfig.Default;
        }
    }
    public class TopicConfig
    {
        public static TopicConfig Default => new();
        public int MaxSizeInMegabytes { get; set; } = 1024;
        public bool EnablePartitioning { get; set; } = false;

    }
    public class SubscriberConfig
    {
        public static SubscriberConfig Default => new();
        public int MaxImmediateRetriesInBatch { get; set; } = 5;
    }
}