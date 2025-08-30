using System;
using System.Collections.Generic;
using System.Threading;

namespace SourceEngineFastDownloadTool
{
    internal class Program
    {
        private static AppConfig Config;
        private static HashSet<string> FileExtensions;
        private static HashSet<string> ProcessedFiles;
        private static bool RunOnce = false;
        private static string CustomConfigPath = "config.json";

        static void Main(string[] args)
        {
            ApplicationInfo.DisplayApplicationInfo();
            Console.WriteLine();
            if (!ParseArguments(args))
            {
                return;
            }
            Config = ConfigManager.LoadConfig(CustomConfigPath);
            FileExtensions = ConfigManager.LoadFileTypes(Config.FileTypes);
            ProcessedFiles = ConfigManager.LoadProcessedFiles(Config.ProcessedFilesPath);

            FileProcessor.DebugLogs = Config.DebugLogs;

            ApplicationInfo.DisplayServerInfo(
                Config.Servers.Count,
                FileExtensions.Count,
                Config.CheckInterval,
                Config.Run24x7,
                Config.DebugLogs
            );
            Console.WriteLine();

            foreach (var server in Config.Servers)
            {
                Console.WriteLine($"  • {server.Name}");
                Console.WriteLine($"    Source: {server.Source}");
                Console.WriteLine($"    Destination: {server.Destination}");
            }
            Console.WriteLine();

            if (!CompressionManager.Initialize())
            {
                Console.WriteLine("ERROR: No compression tools found!");
                Console.WriteLine("Please install 7-Zip or WinRAR and try again.");
                return;
            }

            CompressionManager.DisplayCompressionInfoFormatted();
            Console.WriteLine();

            if (RunOnce || !Config.Run24x7)
            {
                Console.WriteLine("Mode: Process once and exit");
                ProcessOnce();
            }
            else
            {
                Console.WriteLine("Mode: 24x7 Continuous monitoring (Press Ctrl+C to stop)");
                Console.WriteLine("================================================");
                RunProcessingLoop();
            }
        }

        static bool ParseArguments(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "--help":
                    case "-h":
                        ApplicationInfo.DisplayHelp();
                        return false;

                    case "--version":
                    case "-v":
                        ApplicationInfo.DisplayVersion();
                        return false;

                    case "--config":
                        if (i + 1 < args.Length)
                        {
                            CustomConfigPath = args[++i];
                            Console.WriteLine($"Using custom config: {CustomConfigPath}");
                        }
                        else
                        {
                            Console.WriteLine("Error: --config requires a file path");
                            return false;
                        }
                        break;

                    case "--once":
                        RunOnce = true;
                        break;

                    case "--24x7":
                        Config.Run24x7 = true;
                        break;

                    case "--debug":
                        FileProcessor.DebugLogs = true;
                        Console.WriteLine("Debug mode enabled via command line");
                        break;

                    default:
                        Console.WriteLine($"Unknown argument: {args[i]}");
                        Console.WriteLine("Use --help for usage information");
                        return false;
                }
            }
            return true;
        }

        static void ProcessOnce()
        {
            Console.WriteLine("Starting one-time processing...");
            Console.WriteLine();

            bool anyProcessed = ProcessAllServers();

            if (anyProcessed)
            {
                ConfigManager.SaveProcessedFiles(Config.ProcessedFilesPath, ProcessedFiles);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Saved processed files list to: {Config.ProcessedFilesPath}");
            }
            else
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] No new files found in any server.");
            }

            Console.WriteLine();
            Console.WriteLine("Processing completed. Exiting.");
        }

        static bool ProcessAllServers()
        {
            bool anyProcessed = false;

            foreach (var server in Config.Servers)
            {
                ApplicationInfo.DisplayProcessingHeader(server.Name, server.Source, server.Destination);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Starting processing...");

                var startTime = DateTime.Now;
                int processed = FileProcessor.ProcessFiles(
                    server.Source,
                    server.Destination,
                    ProcessedFiles,
                    FileExtensions
                );

                if (processed > 0)
                {
                    anyProcessed = true;
                    var duration = DateTime.Now - startTime;
                    ApplicationInfo.DisplayProcessingResult(server.Name, processed, duration.TotalSeconds);
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] No files to process in {server.Name}");
                }

                Console.WriteLine();
            }

            return anyProcessed;
        }

        static void RunProcessingLoop()
        {
            while (true)
            {
                bool anyProcessed = ProcessAllServers();

                if (anyProcessed)
                {
                    ConfigManager.SaveProcessedFiles(Config.ProcessedFilesPath, ProcessedFiles);
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Saved processed files list to: {Config.ProcessedFilesPath}");
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] No new files found in any server. Sleeping for {Config.CheckInterval} seconds...");
                    Thread.Sleep(Config.CheckInterval * 1000);
                }

                Console.WriteLine();
            }
        }
    }
}