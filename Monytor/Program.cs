﻿using Autofac;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Monytor.Core.Configurations;
using Monytor.Implementation.Collectors;
using Monytor.Startup;
using NLog.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Monytor {
    class Program {
        static IContainer _container;
        static ILogger _logger = null;
        static ManualResetEventSlim _manualReset = new ManualResetEventSlim(false);

        static void Main(string[] args) {
            Console.WriteLine(CommandLine.Text.HeadingInfo.Default);
            Console.WriteLine(CommandLine.Text.CopyrightInfo.Default);
            Console.WriteLine();
            Console.WriteLine("Start application with '--help' for assistance.");
            Console.WriteLine();
            Console.WriteLine("Press <CTRL>+<c> to close the application.");
            Console.WriteLine();

            Console.CancelKeyPress += Console_CancelKeyPress;

            try {
                CreateLoggerForConsole();
                SetupBinder();

                var config = new CollectorConfigCreator();
                var parser = new Parser(x => x.CaseSensitive = false);
                var options = parser.ParseArguments<ConsoleArguments>(args)
                    .WithParsed(o => { if (o.CreateDefaultConfig) config.CreateDefaultConfig(); });

                if (options.Tag == ParserResultType.NotParsed) {
                    goto End;
                }

                var parsedResult = options as Parsed<ConsoleArguments>;

                if (parsedResult.Value.CreateDefaultConfig) {
                    _logger.LogInformation("Default config was created");
                    goto End;
                }

                _container = Bootstrapper.Setup().Result;
                if (!config.HasConfig()) {
                    _logger.LogWarning($"Config file '{config.ConfigFileName}' not found. Create default config.\nUse --help for further assistance.");
                }

                RunAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex) {
                _logger?.LogCritical(ex, "Unknown error");
            }

            End:
            Console.WriteLine("Bye!");
        }

        private static async Task RunAsync() {
            SchedulerStartup scheduler = null;
            try {
                scheduler = _container.Resolve<SchedulerStartup>();
                var collectorConfig = _container.Resolve<CollectorConfig>();

                await scheduler.ConfigScheduler(collectorConfig);

                _logger.LogInformation("Scheduler started");
                _manualReset.Wait();
            }
            catch (Exception e) {
                _logger.LogError(e, "Scheduler error");
            }
            finally {
                scheduler.Dispose();
            }
        }
        
        private static void SetupBinder() {
            new SystemInformationCollector();
        }

        private static void CreateLoggerForConsole() {
            ILoggerFactory loggerFactory = new LoggerFactory();
            loggerFactory.AddNLog(new NLogProviderOptions { CaptureMessageTemplates = true, CaptureMessageProperties = true });
            _logger = loggerFactory.CreateLogger<Program>();
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e) {
            _logger?.LogInformation("Application will be closed.");
            var scheduler = _container.Resolve<SchedulerStartup>();
            scheduler.Dispose();
            _container?.Dispose();
            _logger?.LogInformation("All resources were disposed.");
            _manualReset.Set();
        }
    }
}