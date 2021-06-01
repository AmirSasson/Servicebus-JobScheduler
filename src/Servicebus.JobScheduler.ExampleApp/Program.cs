using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Servicebus.JobScheduler.Core.Bus;
using Servicebus.JobScheduler.Core.Contracts;
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
        static async Task<int> Main(string[] args)
        {
            return await Parser.Default.ParseArguments<ProgramOptions>(args)
              .MapResult(async o =>
              {
                  IConfiguration config = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", false, false)
                    .AddJsonFile("appsettings.overrides.json", false, false)
                    .Build();

                  ILoggerFactory loggerFactory = ConfigureLogger();
                  ILogger logger = loggerFactory.CreateLogger("System");

                  var (done, cts) = ConfigureServer(logger);
                  await startAppWithOptions(o, loggerFactory, logger, config, cts);
                  Block(done, cts);
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
        public static ILoggerFactory ConfigureLogger()
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

        public static (ManualResetEventSlim done, CancellationTokenSource cts) ConfigureServer(ILogger logger)
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

        public static void Block(ManualResetEventSlim done, CancellationTokenSource cts)
        {
            cts.Token.WaitHandle.WaitOne(); // the actual block                

            Task.Delay(2000).Wait();
            // wait for all component to dispose
            done.Set();
            cts.Dispose();
        }

        private static async Task startAppWithOptions(ProgramOptions o, ILoggerFactory loggerFactory, ILogger logger, IConfiguration config, CancellationTokenSource source)
        {
            setConsoleTitle(o);

            logger.LogDebug($"Starting app.. with options: {o.GetDescription()}");

            IMessageBus<Topics, Subscriptions> bus;
            if (o.LocalServiceBus == true)
            {
                logger.LogCritical("Running with local in mem Service bus mock");
                bus = new InMemoryMessageBus<Topics, Subscriptions>(loggerFactory.CreateLogger<InMemoryMessageBus<Topics, Subscriptions>>());
            }
            else
            {
                bus = new AzureServiceBusService<Topics, Subscriptions>(config, loggerFactory.CreateLogger<AzureServiceBusService<Topics, Subscriptions>>(), o.RunId);
            }

            var db = new SimpleFilePerJobDefinitionRepository($"db_{o.RunId}");
            var token = source.Token;

            if (o.SetupServiceBus == true)
            {
                await bus.SetupEntitiesIfNotExist();
            }

            if (o.ShouldRunMode(Subscriptions.JobDefinitionChange_ImmediateScheduleRule))
            {
                await bus.RegisterSubscriber(
                   Topics.JobDefinitionChange,
                   Subscriptions.JobDefinitionChange_ImmediateScheduleRule,
                   concurrencyLevel: 3,
                   simulateFailurePercents: o.HandlingErrorRate,
                   new ScheduleNextRunSubscriber(bus, loggerFactory.CreateLogger<ScheduleNextRunSubscriber>()),
                   null,
                   source);
            }

            if (o.ShouldRunMode(Subscriptions.ReadyToRunJobWindow_Validation))
            {
                await bus.RegisterSubscriber(
                   Topics.ReadyToRunJobWindow,
                   Subscriptions.ReadyToRunJobWindow_Validation,
                   concurrencyLevel: 3,
                   simulateFailurePercents: o.HandlingErrorRate,
                   new WindowValidatorSubscriber(bus, loggerFactory.CreateLogger<WindowValidatorSubscriber>(), db, o.RunId),
                   new RetryPolicy<Topics> { PermanentErrorsTopic = Topics.PermanentErrors, RetryDefinition = new RetryExponential(TimeSpan.FromSeconds(40), TimeSpan.FromMinutes(2), 3) },
                   source);
            }

            if (o.ShouldRunMode(Subscriptions.JobWindowValid_RuleTimeWindowExecution))
            {
                await bus.RegisterSubscriber(
                Topics.JobWindowValid,
                Subscriptions.JobWindowValid_RuleTimeWindowExecution,
                concurrencyLevel: 3,
                simulateFailurePercents: o.HandlingErrorRate,
                new WindowExecutionSubscriber(bus, loggerFactory.CreateLogger<WindowExecutionSubscriber>(), o.ExecErrorRate),
                new RetryPolicy<Topics> { PermanentErrorsTopic = Topics.PermanentErrors, RetryDefinition = new RetryExponential(TimeSpan.FromSeconds(40), TimeSpan.FromMinutes(2), 3) },
                source);
            }

            if (o.ShouldRunMode(Subscriptions.JobWindowValid_ScheduleNextRun))
            {
                await bus.RegisterSubscriber(
                    Topics.JobWindowValid,
                    Subscriptions.JobWindowValid_ScheduleNextRun,
                    concurrencyLevel: 3,
                    simulateFailurePercents: o.HandlingErrorRate,
                    new ScheduleNextRunSubscriber(bus, loggerFactory.CreateLogger<ScheduleNextRunSubscriber>()),
                    null,
                    source);
            }

            if (o.ShouldRunMode(Subscriptions.JobWindowConditionMet_Publish))
            {
                await bus.RegisterSubscriber(
                   Topics.JobWindowConditionMet,
                   Subscriptions.JobWindowConditionMet_Publish,
                   concurrencyLevel: 1,
                   simulateFailurePercents: o.HandlingErrorRate,
                   new PublishSubscriber(loggerFactory.CreateLogger<PublishSubscriber>(), o.RunId),
                    null,
                   source);
            }

            if (o.ShouldRunMode(Subscriptions.JobWindowConditionMet_Suppress))
            {
                await bus.RegisterSubscriber(
                   Topics.JobWindowConditionMet,
                   Subscriptions.JobWindowConditionMet_Suppress,
                   concurrencyLevel: 1,
                   simulateFailurePercents: o.HandlingErrorRate,
                   new RuleSupressorSubscriber(db, loggerFactory.CreateLogger<RuleSupressorSubscriber>()),
                   null,
                   source);
            }
            if (o.ShouldRunMode(Subscriptions.JobWindowConditionNotMet_ScheduleIngestionDelay))
            {
                await bus.RegisterSubscriber(
                   Topics.JobWindowConditionNotMet,
                   Subscriptions.JobWindowConditionNotMet_ScheduleIngestionDelay,
                   concurrencyLevel: 3,
                   simulateFailurePercents: o.HandlingErrorRate,
                   new ScheduleIngestionDelayExecutionSubscriber(bus, loggerFactory.CreateLogger<ScheduleIngestionDelayExecutionSubscriber>()),
                   null,
                   source);
            }



#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed           
            if (o.RunSimulator == true)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                UpsertsClientSimulator.Run(bus, initialRuleCount: 0, delayBetweenUpserts: TimeSpan.FromSeconds(1000), maxConcurrentRules: 1, runId: o.RunId, db, logger, maxUpserts: 1, ruleInterval: TimeSpan.FromMinutes(5), token);
            }
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
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