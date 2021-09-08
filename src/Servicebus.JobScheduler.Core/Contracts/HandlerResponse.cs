using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Servicebus.JobScheduler.Core.Contracts.Messages;

namespace Servicebus.JobScheduler.Core.Contracts
{
    public class HandlerResponse
    {
        public class ContinueWith
        {
            [JsonConverter(typeof(JsonStringEnumConverter))]
            public string TopicToPublish { get; set; }
            public BaseJob Message { get; set; }
            public DateTime? ExecuteOnUtc { get; set; }
        }
        public ContinueWith ContinueWithResult { get; set; }
        public int ResultStatusCode { get; set; }

        public static HandlerResponse FinalOk => new HandlerResponse { ResultStatusCode = 200 };

        public static Task<HandlerResponse> FinalOkAsTask => FinalOk.AsTask();

        public Task<HandlerResponse> AsTask() => Task.FromResult(this);
    }
}