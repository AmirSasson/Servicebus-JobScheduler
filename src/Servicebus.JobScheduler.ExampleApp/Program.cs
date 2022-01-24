using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Servicebus.JobScheduler.Core;
using Servicebus.JobScheduler.Core.Contracts;
using Servicebus.JobScheduler.ExampleApp.Common;
using Servicebus.JobScheduler.ExampleApp.Emulators;
using Servicebus.JobScheduler.ExampleApp.Handlers;
using System;
using System.Linq;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;

namespace Servicebus.JobScheduler.ExampleApp
{
    class Program
    {
        private static IJobScheduler<JobCustomData> _scheduler;

        static async Task<int> Main(string[] args)
        {
            return await Parser.Default.ParseArguments<ProgramOptions>(args)
              .MapResult(async o =>
              {
                  IConfiguration config = buildConfiguration();
                  ILoggerFactory loggerFactory = configureLogger();
                  ILogger logger = loggerFactory.CreateLogger("System");

                  var (done, cts) = configureServer(logger);
                  var engine = await startAppWithOptionsWithDependencyInjection(o, loggerFactory, logger, config, cts);
                  //var engine = await startAppWithOptions(o, loggerFactory, logger, config, cts);
                  blockTillTermination(engine, done, cts);
                  return 0;
              },
              errs =>
              {
                  // parsing unsuccessful; deal with parsing errors
                  var result = -2;
                  Console.WriteLine("errors {0}", errs.Count());
                  if (errs.Any(x => x is HelpRequestedError || x is VersionRequestedError))
                      result = -1;
                  Console.WriteLine("Exit code {0}", result);
                  return Task.FromResult(result);
              });
        }

        private static IConfiguration buildConfiguration()
        {
            var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            return new ConfigurationBuilder()
              .AddJsonFile("appsettings.json", false, false)
              .AddJsonFile($"appsettings.{environmentName}.json", true, false)
              .AddJsonFile("appsettings.overrides.json", true, false)
              .Build();
        }

        private static ILoggerFactory configureLogger()
        {
            var loggerFactory = LoggerFactory.Create(builder => builder
                .SetMinimumLevel(LogLevel.Debug)
                .AddSimpleConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.SingleLine = true;
                    options.TimestampFormat = "hh:mm:ss ";
                    options.ColorBehavior = Environment.UserInteractive ? LoggerColorBehavior.Enabled : LoggerColorBehavior.Disabled;
                })
            );
            return loggerFactory;
        }

        private static (ManualResetEventSlim done, CancellationTokenSource cts) configureServer(ILogger logger)
        {
            var done = new ManualResetEventSlim(false);

            var cts = new CancellationTokenSource();
            // delegate to signal of termination of process
            Action shutdown = () =>
            {
                if (!cts.IsCancellationRequested)
                {
                    logger.LogInformation($"Worker {Environment.MachineName} is shutting down...");
                    cts.Cancel();
                }
                done.Wait();
            };

            // subscribe to SIGTERM or similar events
            var assemblyLoadContext = AssemblyLoadContext.GetLoadContext(typeof(Program).Assembly);
            assemblyLoadContext.Unloading += context => shutdown();

            // subscribe to keyboard interrupt
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                shutdown();
                eventArgs.Cancel = true;
            };

            // start server
            logger.LogInformation($"Worker {Environment.MachineName} started. Press Ctrl+C to shut down.");

            return (done, cts);
        }

        private static void blockTillTermination(IAsyncDisposable engineToDispose, ManualResetEventSlim done, CancellationTokenSource cts)
        {
            cts.Token.WaitHandle.WaitOne(); // the actual block
            engineToDispose.DisposeAsync().ConfigureAwait(false);
            Task.Delay(5000).Wait();
            // wait for all component to dispose
            done.Set();
            cts.Dispose();
        }

        private static async Task<IAsyncDisposable> startAppWithOptionsWithDependencyInjection(ProgramOptions options, ILoggerFactory loggerFactory, ILogger logger, IConfiguration config, CancellationTokenSource source)
        {
            setConsoleTitle(options);

            var db = new SimpleFilePerJobDefinitionRepository($"db_{options.RunId}");

            var host = Host.CreateDefaultBuilder()
                        .ConfigureServices((_, services) =>
                        {
                            services.AddScoped<WindowExecutionSubscriber>((services) => new WindowExecutionSubscriber(loggerFactory.CreateLogger<WindowExecutionSubscriber>(), options.ExecErrorRate, TimeSpan.FromSeconds(1.5)));
                            services.AddScoped<PublishJobResultsSubscriber>((services) => new PublishJobResultsSubscriber(loggerFactory.CreateLogger<PublishJobResultsSubscriber>(), options.RunId, simulateFailurePercents: options.HandlingErrorRate));
                            services.AddScoped<RuleSupressorSubscriber>((services) => new RuleSupressorSubscriber(db, loggerFactory.CreateLogger<RuleSupressorSubscriber>(), simulateFailurePercents: options.HandlingErrorRate));
                            services.AddScoped<ScheduleIngestionDelayExecutionSubscriber>((services) => new ScheduleIngestionDelayExecutionSubscriber(loggerFactory.CreateLogger<ScheduleIngestionDelayExecutionSubscriber>(), simulateFailurePercents: options.HandlingErrorRate, new Lazy<IJobScheduler<JobCustomData>>(() => _scheduler)));
                            services.Configure<ServiceBusConfig>(options => config.GetSection("ServiceBus").Bind(options));
                        })
                        .Build();

            logger.LogDebug($"Starting app.. with options: {options.GetDescription()}");

            var builder = new JobSchedulerBuilder<JobCustomData>()
                .WithHandlersServiceProvider(host.Services)
                .UseSchedulingWorker(options.ShouldRunSchedulingWorkers())
                .WithCancelationSource(source)
                .UseAzureServicePubsubProvider(options.LocalServiceBus == false)                
                .AddRootJobExecuterType<WindowExecutionSubscriber>(
                    concurrencyLevel: 3,
                    new RetryPolicy { PermanentErrorsTopic = Topics.PermanentExecutionErrors.ToString(), RetryDefinition = new RetryExponential(TimeSpan.FromSeconds(40), TimeSpan.FromMinutes(2), 3) },
                    enabled: options.ShouldRunJobExecution())
                .AddSubJobHandlerType<Messages.JobOutput, PublishJobResultsSubscriber>(
                    Topics.JobWindowConditionMet.ToString(),
                    Subscriptions.JobWindowConditionMet_Publish.ToString(),
                    concurrencyLevel: 1,
                    enabled: options.ShouldRunMode(Subscriptions.JobWindowConditionMet_Publish))
                .AddSubJobHandlerType<Messages.JobOutput, RuleSupressorSubscriber>(
                    Topics.JobWindowConditionMet.ToString(),
                    Subscriptions.JobWindowConditionMet_Suppress.ToString(),
                    concurrencyLevel: 1,
                    enabled: options.ShouldRunMode(Subscriptions.JobWindowConditionMet_Suppress))
                .AddSubJobHandlerType<Messages.JobWindowExecutionContext, ScheduleIngestionDelayExecutionSubscriber>(
                    Topics.JobWindowConditionNotMet.ToString(),
                    Subscriptions.JobWindowConditionNotMet_ScheduleIngestionDelay.ToString(),
                    concurrencyLevel: 1,
                    enabled: options.ShouldRunMode(Subscriptions.JobWindowConditionNotMet_ScheduleIngestionDelay))
                .UseJobChangeProvider(db);

            _scheduler = await builder.BuildAsync();

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            if (options.RunSimulator == true)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                UpsertsClientSimulator.Run(_scheduler, initialRuleCount: 1, delayBetweenUpserts: TimeSpan.FromSeconds(1500), maxConcurrentRules: 250, db, logger, maxUpserts: 1, ruleInterval: TimeSpan.FromMinutes(2), source.Token);
            }
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            return _scheduler;
        }

        private static async Task<IAsyncDisposable> startAppWithOptions(ProgramOptions options, ILoggerFactory loggerFactory, ILogger logger, IConfiguration config, CancellationTokenSource source)
        {
            setConsoleTitle(options);


            logger.LogDebug($"Starting app.. with options: {options.GetDescription()}");
            var db = new SimpleFilePerJobDefinitionRepository($"db_{options.RunId}");

            var sbConfig = new ServiceBusConfig();
            config.GetSection("ServiceBus").Bind(sbConfig);
            var sbOptions = new InMemOptions<ServiceBusConfig>(sbConfig);

            var builder = new JobSchedulerBuilder<JobCustomData>()
                .UseLoggerFactory(loggerFactory)
                .UseSchedulingWorker(options.ShouldRunSchedulingWorkers())
                .WithCancelationSource(source)
                .WithConfiguration(sbOptions)
                .UseAzureServicePubsubProvider(options.LocalServiceBus == false)
                .AddRootJobExecuter(
                    new WindowExecutionSubscriber(loggerFactory.CreateLogger<WindowExecutionSubscriber>(), options.ExecErrorRate, TimeSpan.FromSeconds(1.5)),
                    concurrencyLevel: 3,
                    new RetryPolicy { PermanentErrorsTopic = Topics.PermanentExecutionErrors.ToString(), RetryDefinition = new RetryExponential(TimeSpan.FromSeconds(40), TimeSpan.FromMinutes(2), 3) },
                    enabled: options.ShouldRunJobExecution())
                .AddSubJobHandler(
                    Topics.JobWindowConditionMet.ToString(),
                    Subscriptions.JobWindowConditionMet_Publish.ToString(),
                    new PublishJobResultsSubscriber(loggerFactory.CreateLogger<PublishJobResultsSubscriber>(), options.RunId, simulateFailurePercents: options.HandlingErrorRate),
                    concurrencyLevel: 1,
                    enabled: options.ShouldRunMode(Subscriptions.JobWindowConditionMet_Publish))
                .AddSubJobHandler(
                    Topics.JobWindowConditionMet.ToString(),
                    Subscriptions.JobWindowConditionMet_Suppress.ToString(),
                    new RuleSupressorSubscriber(db, loggerFactory.CreateLogger<RuleSupressorSubscriber>(), simulateFailurePercents: options.HandlingErrorRate),
                    concurrencyLevel: 1,
                    enabled: options.ShouldRunMode(Subscriptions.JobWindowConditionMet_Suppress))
                .AddSubJobHandler(
                    Topics.JobWindowConditionNotMet.ToString(),
                    Subscriptions.JobWindowConditionNotMet_ScheduleIngestionDelay.ToString(),
                    new ScheduleIngestionDelayExecutionSubscriber(loggerFactory.CreateLogger<ScheduleIngestionDelayExecutionSubscriber>(), simulateFailurePercents: options.HandlingErrorRate, new Lazy<IJobScheduler<JobCustomData>>(() => _scheduler)),
                    concurrencyLevel: 1,
                    enabled: options.ShouldRunMode(Subscriptions.JobWindowConditionNotMet_ScheduleIngestionDelay))
                .UseJobChangeProvider(db);

            _scheduler = await builder.BuildAsync();

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            if (options.RunSimulator == true)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                UpsertsClientSimulator.Run(_scheduler, initialRuleCount: 1, delayBetweenUpserts: TimeSpan.FromSeconds(1500), maxConcurrentRules: 250, db, logger, maxUpserts: 1, ruleInterval: TimeSpan.FromMinutes(2), source.Token);
            }
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            return _scheduler;
        }

        private static void setConsoleTitle(ProgramOptions o)
        {
            if (o.Modes == null && !o.Modes.Any())
            {
                Console.Title = "Job Scheduler Full Simulator components";
            }
            else
            {
                Console.Title = string.Join(" | ", o.Modes.Select(a => a.ToString().Split("_").Last()));
            }
        }
    }
}
