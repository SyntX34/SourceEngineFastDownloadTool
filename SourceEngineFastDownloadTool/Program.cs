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
        private static volatile bool _shutdownRequested = false;

        static void Main(string[] args)
        {
            Console.CancelKeyPress += Console_CancelKeyPress;
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

            try
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
                Console.WriteLine($"Previously processed files: {ProcessedFiles.Count}");

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
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}");
                if (FileProcessor.DebugLogs)
                {
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                }
            }
            finally
            {
                SaveProcessedFilesIfNeeded();
                CompressionManager.Cleanup();
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

            SaveProcessedFilesIfNeeded();

            if (anyProcessed)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Processing completed. Processed files list updated.");
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
                if (_shutdownRequested)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Shutdown requested, stopping processing...");
                    break;
                }

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
                    try
                    {
                        Directory.CreateDirectory(server.Destination);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ERROR: Failed to create destination directory: {ex.Message}");
                        continue;
                    }
                }

                try
                {
                    int processed = FileProcessor.ProcessFiles(
                        server.Source,
                        server.Destination,
                        ProcessedFiles,
                        FileExtensions,
                        Config.ProcessedFilesPath,
                        server.Name 
                    );

                    if (processed > 0)
                    {
                        anyProcessed = true;
                        var duration = DateTime.Now - startTime;
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Completed {server.Name} in {duration.TotalSeconds:F1}s - Processed {processed} files");
                        
                        SaveProcessedFilesIfNeeded();
                    }
                    else
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] No files to process in {server.Name}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ERROR processing {server.Name}: {ex.Message}");
                    if (FileProcessor.DebugLogs)
                    {
                        Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    }
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
            int cyclesSinceLastSave = 0;
            const int maxCyclesBeforeForceSave = 10;

            while (!_shutdownRequested)
            {
                try
                {
                    bool anyProcessed = ProcessAllServers();

                    if (anyProcessed)
                    {
                        emptyCycles = 0;
                        cyclesSinceLastSave = 0;
                        SaveProcessedFilesIfNeeded();
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Processed files list updated.");
                    }
                    else
                    {
                        emptyCycles++;
                        cyclesSinceLastSave++;

                        if (cyclesSinceLastSave >= maxCyclesBeforeForceSave)
                        {
                            SaveProcessedFilesIfNeeded();
                            cyclesSinceLastSave = 0;
                        }

                        if (emptyCycles % maxEmptyCyclesBeforeLog == 1)
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] No new files found. " +
                                             $"Sleeping for {Config.CheckInterval} seconds... " +
                                             $"(Cycle {emptyCycles})");
                        }
                    }

                    if (!_shutdownRequested)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Waiting {Config.CheckInterval} seconds before next check...");
                        
                        for (int i = 0; i < Config.CheckInterval && !_shutdownRequested; i++)
                        {
                            Thread.Sleep(1000);
                        }

                        Console.WriteLine();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ERROR in processing loop: {ex.Message}");
                    if (FileProcessor.DebugLogs)
                    {
                        Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    }
                    
                    SaveProcessedFilesIfNeeded();
                    
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Restarting loop in 30 seconds...");
                    
                    for (int i = 0; i < 30 && !_shutdownRequested; i++)
                    {
                        Thread.Sleep(1000);
                    }
                }
            }

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Processing loop stopped.");
        }

        static void SaveProcessedFilesIfNeeded()
        {
            if (ProcessedFiles != null && Config != null)
            {
                try
                {
                    bool saved = ConfigManager.SaveProcessedFiles(Config.ProcessedFilesPath, ProcessedFiles);
                    if (saved && FileProcessor.DebugLogs)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Saved {ProcessedFiles.Count} processed files to: {Config.ProcessedFilesPath}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ERROR: Failed to save processed files: {ex.Message}");
                }
            }
        }

        static void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            Console.WriteLine();
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Shutdown signal received (Ctrl+C)...");
            e.Cancel = true;
            _shutdownRequested = true;

            SaveProcessedFilesIfNeeded();
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Data saved. Shutting down gracefully...");

            Thread.Sleep(1000);
            Environment.Exit(0);
        }

        static void CurrentDomain_ProcessExit(object? sender, EventArgs e)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Application exiting...");
            SaveProcessedFilesIfNeeded();
        }
    }
}