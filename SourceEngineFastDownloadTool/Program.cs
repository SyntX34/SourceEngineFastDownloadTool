using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace SourceEngineFastDownloadTool
{
    internal class Program
    {
        private static AppConfig? Config;
        private static HashSet<string>? FileExtensions;
        private static HashSet<string>? ProcessedFiles;
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

            Console.WriteLine($"Configuration loaded from: {CustomConfigPath}");
            Console.WriteLine($"Check Interval: {Config.CheckInterval} seconds");
            Console.WriteLine($"24x7 Mode: {Config.Run24x7}");
            Console.WriteLine($"Debug Logs: {Config.DebugLogs}");
            Console.WriteLine($"Processed Files Path: {Config.ProcessedFilesPath}");
            Console.WriteLine($"File Types: {Config.FileTypes}");
            Console.WriteLine($"Loaded {Config.Servers.Count} server configurations");
            Console.WriteLine($"Watching {FileExtensions.Count} file extensions");

            foreach (var server in Config.Servers)
            {
                Console.WriteLine($"  • {server.Name}");
                Console.WriteLine($"    Source: {server.Source}");
                Console.WriteLine($"    Destination: {server.Destination}");
            }
            Console.WriteLine();

            if (!CompressionManager.Initialize())
            {
                Console.WriteLine("ERROR: Failed to initialize compression tools!");
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
                        if (Config != null) Config.Run24x7 = true;
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

            if (anyProcessed && ProcessedFiles != null && Config != null)
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
            if (Config == null || ProcessedFiles == null || FileExtensions == null)
                return false;

            bool anyProcessed = false;

            foreach (var server in Config.Servers)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Processing {server.Name}...");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Source: {server.Source}");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Destination: {server.Destination}");

                var startTime = DateTime.Now;

                if (!Directory.Exists(server.Source))
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ERROR: Source directory does not exist: {server.Source}");
                    Console.WriteLine();
                    continue;
                }

                if (!Directory.Exists(server.Destination))
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Creating destination directory: {server.Destination}");
                    Directory.CreateDirectory(server.Destination);
                }
                int processed = FileProcessor.ProcessFiles(
                    server.Source,
                    server.Destination,
                    ProcessedFiles,
                    FileExtensions,
                    Config.ProcessedFilesPath
                );

                if (processed > 0)
                {
                    anyProcessed = true;
                    var duration = DateTime.Now - startTime;
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Completed {server.Name} in {duration.TotalSeconds:F1}s - Processed {processed} files");
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
            if (Config == null)
            {
                Console.WriteLine("ERROR: Configuration not loaded!");
                return;
            }

            int emptyCycles = 0;
            const int maxEmptyCyclesBeforeLog = 5;

            while (true)
            {
                try
                {
                    bool anyProcessed = ProcessAllServers();

                    if (anyProcessed && ProcessedFiles != null)
                    {
                        emptyCycles = 0;
                        ConfigManager.SaveProcessedFiles(Config.ProcessedFilesPath, ProcessedFiles);
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Final save of processed files list to: {Config.ProcessedFilesPath}");
                    }
                    else
                    {
                        emptyCycles++;

                        if (emptyCycles % maxEmptyCyclesBeforeLog == 1)
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] No new files found. " +
                                             $"Sleeping for {Config.CheckInterval} seconds... " +
                                             $"(Cycle {emptyCycles})");
                        }
                    }

                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Waiting {Config.CheckInterval} seconds before next check...");
                    for (int i = 0; i < Config.CheckInterval; i++)
                    {
                        if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.C &&
                            (ConsoleModifiers.Control & ConsoleModifiers.Control) != 0)
                        {
                            Console.WriteLine("\n[CTRL+C detected] Shutting down...");
                            return;
                        }
                        Thread.Sleep(1000);
                    }

                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ERROR in processing loop: {ex.Message}");
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Restarting loop in 30 seconds...");
                    Thread.Sleep(30000);
                }
            }
        }
    }
}