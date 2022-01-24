using Microsoft.Extensions.Logging;
using Servicebus.JobScheduler.Core;
using Servicebus.JobScheduler.Core.Contracts;
using Servicebus.JobScheduler.Core.Utils;
using Servicebus.JobScheduler.ExampleApp.Common;
using Servicebus.JobScheduler.ExampleApp.Messages;
using System;
using System.Threading.Tasks;

namespace Servicebus.JobScheduler.ExampleApp.Handlers
{
    public class ScheduleIngestionDelayExecutionSubscriber : BaseSimulatorHandler<JobWindowExecutionContext>
    {
        private readonly ILogger _logger;
        private readonly Lazy<IJobScheduler> _scheduler;
        const int INGESTION_TOLLERANCE_DELAY_MINUTES = 2;
        readonly TimeSpan _ingestionDelay = TimeSpan.FromMinutes(INGESTION_TOLLERANCE_DELAY_MINUTES);

        public ScheduleIngestionDelayExecutionSubscriber(ILogger<ScheduleIngestionDelayExecutionSubscriber> logger, int simulateFailurePercents, Lazy<IJobScheduler> scheduler)
        : base(simulateFailurePercents, TimeSpan.Zero, logger)
        {
            _logger = logger;
            _scheduler = scheduler;
        }
        protected override async Task<HandlerResponse> handlePrivate(JobWindowExecutionContext msg)
        {
            // clone
            var delayedIngestionExecutionTime = msg.ToTime.Add(_ingestionDelay);
            if (msg.WindowExecutionTime >= delayedIngestionExecutionTime)
            {
                _logger.LogWarning($"No need to schedule delayed execution for window  ({msg.FromTime} -> {msg.ToTime}) to later, cause the window already executed late! WindowExecutionTime: {msg.WindowExecutionTime}, delayedIngestionExecutionTime: {delayedIngestionExecutionTime}");
            }
            else if (msg.Schedule.PeriodicJob)
            {
                var delayedWindow = msg.Clone();

                delayedWindow.SkipNextWindowValidation = true;

                var laterExecutionTime = delayedIngestionExecutionTime;
                _logger.LogWarning($"Scheduling same job ({msg.FromTime} -> {msg.ToTime}) to later {laterExecutionTime} WindowExecutionTime: {msg.WindowExecutionTime}, delayedIngestionExecutionTime: {delayedIngestionExecutionTime}");
                await _scheduler.Value.ScheduleOnce(delayedWindow, laterExecutionTime);
            }

            return HandlerResponse.FinalOk;
        }
    }
}
