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
        private readonly IMessageBus<Topics, Subscriptions> _bus;
        private readonly ILogger _logger;
        const int INGESTION_TOLLERANCE_DELAY_MINUTES = 2;
        readonly TimeSpan _ingestionDelay = TimeSpan.FromMinutes(INGESTION_TOLLERANCE_DELAY_MINUTES);

        public ScheduleIngestionDelayExecutionSubscriber(IMessageBus<Topics, Subscriptions> bus, ILogger<ScheduleIngestionDelayExecutionSubscriber> logger, int simulateFailurePercents)
        : base(simulateFailurePercents, TimeSpan.Zero, logger)
        {
            _logger = logger;
            _bus = bus;
        }
        protected override async Task<bool> handlePrivate(JobWindowExecutionContext msg)
        {
            // clone
            var delayedWindow = msg.Clone();
            delayedWindow.RunInIntervals = false;
            delayedWindow.SkipValidation = true;
            var delayedIngestionExecutionTime = msg.ToTime.Add(_ingestionDelay);
            if (msg.WindowExecutionTime >= delayedIngestionExecutionTime)
            {
                _logger.LogWarning($"No need to schedule delayed execution cause the window already executed late! WindowExecutionTime: {msg.WindowExecutionTime}, delayedIngestionExecutionTime: {delayedIngestionExecutionTime}");
            }
            else if (msg.RunInIntervals)
            {
                await _bus.PublishAsync(delayedWindow, Topics.ReadyToRunJobWindow, DateTime.UtcNow.Add(_ingestionDelay));
            }

            return true;
        }
    }
}
