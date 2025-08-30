using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SourceEngineFastDownloadTool
{
    public static class FileProcessor
    {
        public static bool DebugLogs { get; set; } = false;

        private const int BATCH_SIZE = 50;
        private static readonly int MAX_PARALLEL_TASKS = Math.Max(1, Environment.ProcessorCount / 2);

        public static bool IsFileInUse(string filePath)
        {
            try
            {
                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    return false;
                }
            }
            catch (IOException)
            {
                if (DebugLogs) Console.WriteLine($"  [DEBUG] File in use: {filePath}");
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsFileGrowing(string filePath)
        {
            try
            {
                long size1 = new FileInfo(filePath).Length;
                Thread.Sleep(100);
                long size2 = new FileInfo(filePath).Length;
                bool isGrowing = size1 != size2;

                if (DebugLogs && isGrowing)
                    Console.WriteLine($"  [DEBUG] File is growing: {filePath} ({size1} -> {size2} bytes)");

                return isGrowing;
            }
            catch
            {
                return true;
            }
        }

        public static List<string> GetAllFiles(string directory, HashSet<string> extensions)
        {
            var files = new List<string>();

            try
            {
                directory = NormalizePath(directory);
                
                if (Directory.Exists(directory))
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Scanning directory: {directory}");
                    
                    var allFiles = Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
                        .Where(file => extensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                        .Select(NormalizePath)
                        .ToList();

                    files.AddRange(allFiles);

                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Found {files.Count} matching files");
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Directory not found: {directory}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Error scanning directory {directory}: {ex.Message}");
            }

            return files;
        }

        public static int ProcessFiles(string sourceDir, string destDir, HashSet<string> processedFiles, HashSet<string> extensions, string processedFilesPath, string serverName = "")
        {
            sourceDir = NormalizePath(sourceDir);
            destDir = NormalizePath(destDir);

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] === Starting Full Scan for {serverName} ===");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Source: {sourceDir}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Destination: {destDir}");

            if (!Directory.Exists(sourceDir))
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ERROR: Source directory does not exist: {sourceDir}");
                return 0;
            }

            var stopwatch = Stopwatch.StartNew();
            var allFiles = GetAllFiles(sourceDir, extensions);
            stopwatch.Stop();
            
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Complete file scan finished in {stopwatch.Elapsed.TotalSeconds:F1}s");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Total files found: {allFiles.Count}");

            if (allFiles.Count == 0)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] No files to process for {serverName}");
                return 0;
            }
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Filtering already processed files...");
            var unprocessedFiles = new List<string>();
            int skippedAlreadyProcessed = 0;

            foreach (var file in allFiles)
            {
                string processedKey = GetProcessedFileKey(file, sourceDir, serverName);
                if (processedFiles.Contains(processedKey))
                {
                    skippedAlreadyProcessed++;
                    if (DebugLogs)
                        Console.WriteLine($"  [DEBUG] Skipping already processed: {processedKey}");
                }
                else
                {
                    unprocessedFiles.Add(file);
                }
            }

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Files already processed (skipped): {skippedAlreadyProcessed}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Files to check: {unprocessedFiles.Count}");

            if (unprocessedFiles.Count == 0)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] All files already processed for {serverName}");
                return 0;
            }

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Checking destination for missing compressed files...");
            var filesToCompress = new List<FileCompressionTask>();
            var existingCompressedFiles = 0;

            stopwatch = Stopwatch.StartNew();
            
            foreach (var file in unprocessedFiles)
            {
                string relativePath = GetRelativePath(file, sourceDir);
                string destPath = Path.Combine(destDir, relativePath);
                destPath = NormalizePath(destPath);

                string fileName = Path.GetFileName(destPath);
                string? directory = Path.GetDirectoryName(destPath);

                if (!string.IsNullOrEmpty(directory))
                {
                    string compressedDestPath = Path.Combine(directory, fileName + ".bz2");
                    compressedDestPath = NormalizePath(compressedDestPath);

                    if (File.Exists(compressedDestPath))
                    {
                        existingCompressedFiles++;
                        lock (processedFiles)
                        {
                            string processedKey = GetProcessedFileKey(file, sourceDir, serverName);
                            processedFiles.Add(processedKey);
                        }
                        
                        if (DebugLogs)
                            Console.WriteLine($"  [DEBUG] Compressed file exists: {compressedDestPath}");
                    }
                    else
                    {
                        filesToCompress.Add(new FileCompressionTask
                        {
                            SourcePath = file,
                            DestinationPath = compressedDestPath,
                            RelativePath = relativePath,
                            ProcessedKey = GetProcessedFileKey(file, sourceDir, serverName)
                        });
                        
                        if (DebugLogs)
                            Console.WriteLine($"  [DEBUG] Missing compressed file: {compressedDestPath}");
                    }
                }
            }

            stopwatch.Stop();
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Destination check completed in {stopwatch.Elapsed.TotalSeconds:F1}s");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Compressed files already exist: {existingCompressedFiles}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Files needing compression: {filesToCompress.Count}");

            if (filesToCompress.Count == 0)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] No files need compression for {serverName}");
                ConfigManager.SaveProcessedFiles(processedFilesPath, processedFiles);
                return 0;
            }

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] === Starting Compression Phase ===");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Compressing {filesToCompress.Count} files...");

            int processedCount = CompressFiles(filesToCompress, processedFiles, serverName);

            if (processedCount > 0 || existingCompressedFiles > 0)
            {
                ConfigManager.SaveProcessedFiles(processedFilesPath, processedFiles);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Updated processed files list");
            }

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] === Compression Phase Complete ===");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Successfully compressed: {processedCount} files");

            return processedCount;
        }

        private static int CompressFiles(List<FileCompressionTask> tasks, HashSet<string> processedFiles, string serverName)
        {
            int processedCount = 0;
            int totalTasks = tasks.Count;
            int completedTasks = 0;

            var batches = tasks
                .Select((task, index) => new { task, index })
                .GroupBy(x => x.index / BATCH_SIZE)
                .Select(g => g.Select(x => x.task).ToList())
                .ToList();

            var stopwatch = Stopwatch.StartNew();

            foreach (var batch in batches)
            {
                var batchResults = new ConcurrentBag<bool>();

                Parallel.ForEach(batch, new ParallelOptions { MaxDegreeOfParallelism = MAX_PARALLEL_TASKS }, task =>
                {
                    bool success = ProcessCompressionTask(task, processedFiles, serverName);
                    batchResults.Add(success);
                });

                int batchSuccess = batchResults.Count(r => r);
                processedCount += batchSuccess;
                completedTasks += batch.Count;

                double elapsed = stopwatch.Elapsed.TotalSeconds;
                double rate = completedTasks / elapsed;
                double eta = (totalTasks - completedTasks) / rate;

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Batch completed: {completedTasks}/{totalTasks} " +
                                $"({batchSuccess}/{batch.Count} successful) - " +
                                $"Rate: {rate:F1}/sec - ETA: {eta:F0}s");

                if (completedTasks % (BATCH_SIZE * 5) == 0)
                {
                    ConfigManager.SaveProcessedFiles("processed_files.txt", processedFiles);
                    if (DebugLogs)
                        Console.WriteLine($"  [DEBUG] Saved progress: {processedFiles.Count} processed files");
                }
            }

            stopwatch.Stop();
            double totalRate = totalTasks / stopwatch.Elapsed.TotalSeconds;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Compression completed in {stopwatch.Elapsed.TotalSeconds:F1}s " +
                            $"(Overall rate: {totalRate:F1} files/sec)");

            return processedCount;
        }

        private static bool ProcessCompressionTask(FileCompressionTask task, HashSet<string> processedFiles, string serverName)
        {
            try
            {
                if (IsFileInUse(task.SourcePath) || IsFileGrowing(task.SourcePath))
                {
                    if (DebugLogs)
                        Console.WriteLine($"  [DEBUG] File in use or growing, skipping: {task.SourcePath}");
                    return false;
                }

                string? directory = Path.GetDirectoryName(task.DestinationPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (DebugLogs)
                    Console.WriteLine($"  [DEBUG] Compressing: {task.SourcePath} -> {task.DestinationPath}");

                if (CompressionManager.CompressFile(task.SourcePath, task.DestinationPath))
                {
                    SetLinuxPermissions(task.DestinationPath);

                    lock (processedFiles)
                    {
                        processedFiles.Add(task.ProcessedKey);
                    }

                    if (DebugLogs)
                        Console.WriteLine($"  [DEBUG] Successfully compressed: {task.SourcePath}");

                    return true;
                }
                else
                {
                    if (DebugLogs)
                        Console.WriteLine($"  [DEBUG] Compression failed: {task.SourcePath}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Error compressing {task.SourcePath}: {ex.Message}");
                return false;
            }
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            if (PlatformUtils.IsWindows)
            {
                return path.Replace('/', '\\');
            }
            else
            {
                return path.Replace('\\', '/');
            }
        }

        private static string GetProcessedFileKey(string filePath, string sourceDir, string serverName = "")
        {
            string relativePath = GetRelativePath(filePath, sourceDir);
            
            if (!string.IsNullOrEmpty(serverName))
            {
                return $"{serverName}:{relativePath}";
            }
            
            return relativePath;
        }

        private static string GetRelativePath(string fullPath, string basePath)
        {
            fullPath = NormalizePath(fullPath);
            basePath = NormalizePath(basePath);

            if (!basePath.EndsWith(Path.DirectorySeparatorChar.ToString()) && 
                !basePath.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            {
                basePath += Path.DirectorySeparatorChar;
            }

            if (fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath.Substring(basePath.Length);
            }

            try
            {
                var fullUri = new Uri(fullPath);
                var baseUri = new Uri(basePath);
                var relativeUri = baseUri.MakeRelativeUri(fullUri);
                return Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
            }
            catch
            {
                return fullPath;
            }
        }

        private static void SetLinuxPermissions(string filePath)
        {
            try
            {
                if (PlatformUtils.IsLinux || PlatformUtils.IsOSX)
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "chmod",
                            Arguments = $"777 \"{filePath}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        }
                    };

                    process.Start();
                    process.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                if (DebugLogs)
                    Console.WriteLine($"  [DEBUG] Error setting permissions: {ex.Message}");
            }
        }

        public static void ClearDestinationCache()
        {
        }
    }

    public class ProcessResult
    {
        public bool WasProcessed { get; set; }
        public bool ShouldSkipInFuture { get; set; }
    }

    public class BatchProcessResult
    {
        public int ProcessedCount { get; set; } = 0;
        public int SkippedCount { get; set; } = 0;
        public bool HasNewProcessedFiles { get; set; } = false;
    }

    public class FileCompressionTask
    {
        public string SourcePath { get; set; } = "";
        public string DestinationPath { get; set; } = "";
        public string RelativePath { get; set; } = "";
        public string ProcessedKey { get; set; } = "";
    }
}