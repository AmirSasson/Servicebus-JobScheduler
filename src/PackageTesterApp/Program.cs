using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Servicebus.JobScheduler.Core;
using Servicebus.JobScheduler.Core.Contracts;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PackageTesterApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // For complete usage example see README on root or Servicebus.JobScheduler.ExampleApp.Program.cs
            var cancelToken = new CancellationTokenSource();

            await bootApp(cancelToken);
            Console.ReadKey();
            cancelToken.Cancel();
            await Task.Delay(TimeSpan.FromSeconds(3));
        }

        private static async Task<IAsyncDisposable> bootApp(CancellationTokenSource cancelToken)
        {
            var host = Host.CreateDefaultBuilder()
                        .ConfigureServices((_, services) =>
                        {
                            services.AddScoped((services) => new EchoWindowExecutionSubscriber());
                        })
                        .Build();

            var builder = new JobSchedulerBuilder()
                .WithHandlersServiceProvider(host.Services)
                .UseSchedulingWorker()
                .WithCancelationSource(cancelToken)
                .UseInMemoryPubsubProvider(true)
                .AddRootJobExecuterType<EchoWindowExecutionSubscriber, JobData>(
                    concurrencyLevel: 3,
                    new RetryPolicy { PermanentErrorsTopic = "TestPermamantErrors", RetryDefinition = new RetryExponential(TimeSpan.FromSeconds(40), TimeSpan.FromMinutes(2), 3) }
                    )
               ;

            var scheduler = await builder.BuildAsync();

            seed(scheduler, cancelToken);

            return scheduler;
        }

        private static Task seed(IJobScheduler scheduler, CancellationTokenSource cancelToken)
        {
            return Task.Run(async () =>
            {
                for (int i = 0; i < 20 && !cancelToken.IsCancellationRequested; i++)
                {
                    await scheduler.ScheduleJob(
                        new Job<JobData>
                        {
                            Id = i.ToString(),
                            Payload = new JobData { Name = $"Name {i}" },
                            Schedule = new JobSchedule { PeriodicJob = true, RunIntervalSeconds = 30 }
                        });
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }
            });
        }
    }
    public class JobData
    {
        public string Name { get; set; }
    }
}
