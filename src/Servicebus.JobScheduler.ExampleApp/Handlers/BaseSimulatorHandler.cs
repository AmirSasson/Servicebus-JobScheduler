using Microsoft.Extensions.Logging;
using Servicebus.JobScheduler.Core.Contracts;
using Servicebus.JobScheduler.Core.Contracts.Messages;
using System;
using System.Threading.Tasks;

namespace Servicebus.JobScheduler.ExampleApp.Handlers
{
    public abstract class BaseSimulatorHandler<TMsg> : IMessageHandler<TMsg>
        where TMsg : class, IJob
    {
        private readonly int _simulateFailurePercents;
        private readonly TimeSpan _simulateExecutionTime;
        private readonly ILogger _logger;
        Random _rand = new Random((int)DateTime.Now.Ticks);

        public BaseSimulatorHandler(int simulateFailurePercents, TimeSpan simulateExecutionTime, ILogger logger)
        {
            _simulateFailurePercents = simulateFailurePercents;
            _simulateExecutionTime = simulateExecutionTime;
            _logger = logger;
        }
        abstract protected Task<HandlerResponse> handlePrivate(TMsg msg);


        public async Task<HandlerResponse> Handle(TMsg msg)
        {
            bool shouldSimulateError()
            {
                //return counterDummy < 35 && rand.Next(0, 100) <= _simulateFailurePercents;
                return _rand.Next(0, 100) <= _simulateFailurePercents;
            }
            _logger.LogWarning($"Handling msg {msg.Id} ...");

            await Task.Delay(_simulateExecutionTime);
            if (shouldSimulateError())
            {
                throw new ApplicationException("Simulate processing Error ..");
            }
            return await handlePrivate(msg);
        }
    }
}
