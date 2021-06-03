using Microsoft.Extensions.Logging;
using Servicebus.JobScheduler.Core.Contracts;
using Servicebus.JobScheduler.Core.Contracts.Messages;
using System;
using System.Threading.Tasks;

namespace Servicebus.JobScheduler.ExampleApp.Handlers
{
    public abstract class BaseSimulatorHandler<T> : IMessageHandler<T>
        where T : class, IMessageBase
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
        abstract protected Task<bool> handlePrivate(T msg);
        public async Task<bool> Handle(T msg)
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
