using Servicebus.JobScheduler.Core.Contracts;
using System.Threading.Tasks;

namespace PackageTesterApp
{
    public class EchoWindowExecutionSubscriber : IMessageHandler<JobWindow<JobData>>
    {
        public Task<HandlerResponse> Handle(JobWindow<JobData> msg)
        {
            System.Console.WriteLine(msg.Payload.Name);
            return HandlerResponse.FinalOkAsTask;
        }
    }
}
