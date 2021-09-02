using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Servicebus.JobScheduler.Core.Contracts.Messages;

namespace Servicebus.JobScheduler.Core.Contracts
{
    public class HandlerResponse<TTopics> where TTopics : struct, Enum
    {
        public class ContinueWith
        {
            [JsonConverter(typeof(JsonStringEnumConverter))]
            public TTopics TopicToPublish { get; set; }
            public IMessageBase Message { get; set; }
            public DateTime? ExecuteOnUtc { get; set; }
        }
        public ContinueWith ContinueWithResult { get; set; }
        public int ResultStatusCode { get; set; }

        public static HandlerResponse<TTopics> FinalOk => new HandlerResponse<TTopics> { ResultStatusCode = 200 };

        public static Task<HandlerResponse<TTopics>> FinalOkAsTask => FinalOk.AsTask();

        public Task<HandlerResponse<TTopics>> AsTask() => Task.FromResult(this);
    }
}