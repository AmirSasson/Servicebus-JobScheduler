using Microsoft.Extensions.Logging;
using Servicebus.JobScheduler.Core.Contracts;
using Servicebus.JobScheduler.Core.Utils;
using Servicebus.JobScheduler.ExampleApp.Messages;
using System;
using System.Threading.Tasks;

namespace Servicebus.JobScheduler.ExampleApp.Handlers
{
    public class ScheduleIngestionDelayExecutionSubscriber : BaseSimulatorHandler<JobWindowExecutionContext>
    {
        private readonly ILogger _logger;
        const int INGESTION_TOLLERANCE_DELAY_MINUTES = 2;
        readonly TimeSpan _ingestionDelay = TimeSpan.FromMinutes(INGESTION_TOLLERANCE_DELAY_MINUTES);

        public ScheduleIngestionDelayExecutionSubscriber(ILogger<ScheduleIngestionDelayExecutionSubscriber> logger, int simulateFailurePercents)
        : base(simulateFailurePercents, TimeSpan.Zero, logger)
        {
            _logger = logger;
        }
        protected override Task<HandlerResponse<Topics>> handlePrivate(JobWindowExecutionContext msg)
        {
            // clone
            var delayedWindow = msg.Clone();
            delayedWindow.Schedule.PeriodicJob = false;
            delayedWindow.SkipNextWindowValidation = true;
            var delayedIngestionExecutionTime = msg.ToTime.Add(_ingestionDelay);
            if (msg.WindowExecutionTime >= delayedIngestionExecutionTime)
            {
                _logger.LogWarning($"No need to schedule delayed execution cause the window already executed late! WindowExecutionTime: {msg.WindowExecutionTime}, delayedIngestionExecutionTime: {delayedIngestionExecutionTime}");
            }
            else if (msg.Schedule.PeriodicJob)
            {
                return new HandlerResponse<Topics> { ResultStatusCode = 200, ContinueWithResult = new HandlerResponse<Topics>.ContinueWith { Message = delayedWindow, TopicToPublish = Topics.ReadyToRunJobWindow, ExecuteOnUtc = DateTime.UtcNow.Add(_ingestionDelay) } }.AsTask();
            }

            return HandlerResponse<Topics>.FinalOkAsTask;
        }
    }
}
