using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Servicebus.JobScheduler.ExampleApp
{
    public class ProgramOptions
    {
        [Option('a', "all-modes", HelpText = "Run All modes", Default = true)]
        public bool? RunAll { get; set; }

        [Option('m', "modes", Required = false, HelpText = "Run Subscriptions modes, default is ALL, setting this option will overrides the 'all-modes' option")]
        public IEnumerable<Subscriptions> Modes { get; set; }

        [Option('t', "tester-simulator", HelpText = "Run Tester simulator to generate upserts", Default = true)]
        public bool? RunSimulator { get; set; }

        [Option('s', "run-setup", HelpText = "Run Service bus setup", Default = true)]
        public bool? SetupServiceBus { get; set; }

        [Option('e', "error-rate", HelpText = "Handlers Error simulation rate (percents)", Default = -1)]
        public int HandlingErrorRate { get; set; }

        [Option('r', "run-id", Required = false, HelpText = "Run Id so other runs will be ignored in this run", Default = "my-run")]
        public string RunId { get; set; }

        [Option('l', "local-bus", HelpText = "Run local in memory service bus emulator", Default = false)]
        public bool? LocalServiceBus { get; set; }

        [Option('x', "execute-error-rate", HelpText = "Handlers Error simulation rate (percents)", Default = -1)]
        public int ExecErrorRate { get;  set; }

        public string GetDescription()
        {
            JsonSerializerOptions options = new()
            {
                Converters = { new JsonStringEnumConverter() }
            };
            return JsonSerializer.Serialize(this, options);
        }

        public bool ShouldRunMode(Subscriptions subscription)
        {
            if (Modes.Any())
            {
                return Modes.Any() && Modes.Contains(subscription);
            }
            return RunAll == true;
        }
    }
}
