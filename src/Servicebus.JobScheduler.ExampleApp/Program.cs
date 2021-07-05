using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Servicebus.JobScheduler.Core.Bus;
using Servicebus.JobScheduler.Core.Contracts;
using Servicebus.JobScheduler.ExampleApp.Common;
using Servicebus.JobScheduler.ExampleApp.Emulators;
using Servicebus.JobScheduler.ExampleApp.Handlers;
using Servicebus.JobScheduler.ExampleApp.Messages;
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
                  var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
                  IConfiguration config = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", false, false)
                    .AddJsonFile($"appsettings.{environmentName}.json", true, false)
                    .AddJsonFile("appsettings.overrides.json", true, false)
                    .Build();

                  ILoggerFactory loggerFactory = ConfigureLogger();
                  ILogger logger = loggerFactory.CreateLogger("System");

                  var (done, cts) = ConfigureServer(logger);
                  var engine = await startAppWithOptions(o, loggerFactory, logger, config, cts);
                  Block(engine, done, cts);
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

        public static void Block(IAsyncDisposable engineToDispose, ManualResetEventSlim done, CancellationTokenSource cts)
        {
            cts.Token.WaitHandle.WaitOne(); // the actual block
            engineToDispose.DisposeAsync().ConfigureAwait(false);
            Task.Delay(5000).Wait();
            // wait for all component to dispose
            done.Set();
            cts.Dispose();
        }

        private static async Task<IAsyncDisposable> startAppWithOptions(ProgramOptions o, ILoggerFactory loggerFactory, ILogger logger, IConfiguration config, CancellationTokenSource source)
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

            IRepository<JobDefinition> db = new SimpleFilePerJobDefinitionRepository($"db_{o.RunId}");
            var token = source.Token;

            if (o.SetupServiceBus == true)
            {
                await bus.SetupEntitiesIfNotExist(config);
            }

            await buildTopicFlowTree(o, loggerFactory, source, bus, db);



#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            if (o.RunSimulator == true)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                UpsertsClientSimulator.Run(bus, initialRuleCount: 1, delayBetweenUpserts: TimeSpan.FromSeconds(1500), maxConcurrentRules: 250, runId: o.RunId, db, logger, maxUpserts: 1, ruleInterval: TimeSpan.FromMinutes(5), token);
            }
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            return bus;
        }

        /// <summary>
        /// this build the topic tree described in the TopicFLow.vsdx
        /// </summary>
        /// <param name="o">the options</param>
        /// <param name="loggerFactory">logger Factory</param>
        /// <param name="source">app Cancellation Token Source</param>
        /// <param name="bus">message bus refrence</param>
        /// <param name="db"></param>
        /// <returns></returns>
        private static async Task buildTopicFlowTree(ProgramOptions o, ILoggerFactory loggerFactory, CancellationTokenSource source, IMessageBus<Topics, Subscriptions> bus, IRepository<JobDefinition> db)
        {
            if (o.ShouldRunMode(Subscriptions.JobDefinitionChange_ImmediateScheduleRule))
            {
                await bus.RegisterSubscriber(
                   Topics.JobDefinitionChange,
                   Subscriptions.JobDefinitionChange_ImmediateScheduleRule,
                   concurrencyLevel: 3,
                   new ScheduleNextRunSubscriber(bus, loggerFactory.CreateLogger<ScheduleNextRunSubscriber>(), simulateFailurePercents: o.HandlingErrorRate),
                   null,
                   source);
            }

            if (o.ShouldRunMode(Subscriptions.ReadyToRunJobWindow_Validation))
            {
                await bus.RegisterSubscriber(
                   Topics.ReadyToRunJobWindow,
                   Subscriptions.ReadyToRunJobWindow_Validation,
                   concurrencyLevel: 3,
                   new WindowValidatorSubscriber(bus, loggerFactory.CreateLogger<WindowValidatorSubscriber>(), db, o.RunId, simulateFailurePercents: o.HandlingErrorRate),
                   new RetryPolicy<Topics> { PermanentErrorsTopic = Topics.PermanentErrors, RetryDefinition = new RetryExponential(TimeSpan.FromSeconds(40), TimeSpan.FromMinutes(2), 3) },
                   source);
            }

            if (o.ShouldRunMode(Subscriptions.JobWindowValid_RuleTimeWindowExecution))
            {
                await bus.RegisterSubscriber(
                Topics.JobWindowValid,
                Subscriptions.JobWindowValid_RuleTimeWindowExecution,
                concurrencyLevel: 3,
                new WindowExecutionSubscriber(bus, loggerFactory.CreateLogger<WindowExecutionSubscriber>(), o.ExecErrorRate, TimeSpan.FromSeconds(1.5)),
                new RetryPolicy<Topics> { PermanentErrorsTopic = Topics.PermanentErrors, RetryDefinition = new RetryExponential(TimeSpan.FromSeconds(40), TimeSpan.FromMinutes(2), 3) },
                source);
            }

            if (o.ShouldRunMode(Subscriptions.JobWindowValid_ScheduleNextRun))
            {
                await bus.RegisterSubscriber(
                    Topics.JobWindowValid,
                    Subscriptions.JobWindowValid_ScheduleNextRun,
                    concurrencyLevel: 3,
                    new ScheduleNextRunSubscriber(bus, loggerFactory.CreateLogger<ScheduleNextRunSubscriber>(), simulateFailurePercents: o.HandlingErrorRate),
                    null,
                    source);
            }

            if (o.ShouldRunMode(Subscriptions.JobWindowConditionMet_Publish))
            {
                await bus.RegisterSubscriber(
                   Topics.JobWindowConditionMet,
                   Subscriptions.JobWindowConditionMet_Publish,
                   concurrencyLevel: 1,
                   new PublishSubscriber(loggerFactory.CreateLogger<PublishSubscriber>(), o.RunId, simulateFailurePercents: o.HandlingErrorRate),
                    null,
                   source);
            }

            if (o.ShouldRunMode(Subscriptions.JobWindowConditionMet_Suppress))
            {
                await bus.RegisterSubscriber(
                   Topics.JobWindowConditionMet,
                   Subscriptions.JobWindowConditionMet_Suppress,
                   concurrencyLevel: 1,
                   new RuleSupressorSubscriber(db, loggerFactory.CreateLogger<RuleSupressorSubscriber>(), simulateFailurePercents: o.HandlingErrorRate),
                   null,
                   source);
            }
            if (o.ShouldRunMode(Subscriptions.JobWindowConditionNotMet_ScheduleIngestionDelay))
            {
                await bus.RegisterSubscriber(
                   Topics.JobWindowConditionNotMet,
                   Subscriptions.JobWindowConditionNotMet_ScheduleIngestionDelay,
                   concurrencyLevel: 3,
                   new ScheduleIngestionDelayExecutionSubscriber(bus, loggerFactory.CreateLogger<ScheduleIngestionDelayExecutionSubscriber>(), simulateFailurePercents: o.HandlingErrorRate),
                   null,
                   source);
            }
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
