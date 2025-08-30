using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace SourceEngineFastDownloadTool
{
    public static class FileProcessor
    {
        public static bool DebugLogs { get; set; } = false;

        public static bool IsFileInUse(string filePath)
        {
            try
            {
                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
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
                Thread.Sleep(500);
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
                if (Directory.Exists(directory))
                {
                    files.AddRange(Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories)
                        .Where(file => extensions.Contains(Path.GetExtension(file).ToLower())));

                    if (DebugLogs)
                        Console.WriteLine($"  [DEBUG] Found {files.Count} files in {directory}");
                }
                else
                {
                    if (DebugLogs)
                        Console.WriteLine($"  [DEBUG] Directory not found: {directory}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scanning directory {directory}: {ex.Message}");
            }

            return files;
        }

        public static void DisplayDebugInfo(string sourceDir, HashSet<string> extensions)
        {
            Console.WriteLine($"=== DEBUG INFO ===");
            Console.WriteLine($"Source Directory: {sourceDir}");
            Console.WriteLine($"Directory Exists: {Directory.Exists(sourceDir)}");

            if (Directory.Exists(sourceDir))
            {
                try
                {
                    var allFiles = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories);
                    Console.WriteLine($"Total Files Found: {allFiles.Length}");

                    var matchingFiles = allFiles.Where(file => extensions.Contains(Path.GetExtension(file).ToLower())).ToList();
                    Console.WriteLine($"Matching Files: {matchingFiles.Count}");

                    if (matchingFiles.Count > 0)
                    {
                        Console.WriteLine("First 10 matching files:");
                        foreach (var file in matchingFiles.Take(10))
                        {
                            Console.WriteLine($"  - {file}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error scanning directory: {ex.Message}");
                }
            }
            Console.WriteLine($"==================");
        }

        public static int ProcessFiles(string sourceDir, string destDir, HashSet<string> processedFiles, HashSet<string> extensions, string processedFilesPath)
        {
            int processedCount = 0;
            bool hasNewProcessedFiles = false;

            if (DebugLogs)
            {
                DisplayDebugInfo(sourceDir, extensions);
            }

            if (!Directory.Exists(sourceDir))
            {
                if (DebugLogs)
                    Console.WriteLine($"  [DEBUG] Source directory does not exist: {sourceDir}");
                return 0;
            }

            var files = GetAllFiles(sourceDir, extensions);

            if (DebugLogs)
                Console.WriteLine($"  [DEBUG] Processing {files.Count} files from {sourceDir}");

            foreach (var file in files)
            {
                var result = ProcessFile(file, sourceDir, destDir, processedFiles);

                if (result.WasProcessed)
                {
                    processedCount++;
                    hasNewProcessedFiles = true;

                    if (processedCount % 10 == 0)
                    {
                        Console.WriteLine($"Processed {processedCount} files...");
                        ConfigManager.SaveProcessedFiles(processedFilesPath, processedFiles);
                    }
                }

                if (result.ShouldSkipInFuture)
                {
                    hasNewProcessedFiles = true;
                }
            }

            if (hasNewProcessedFiles)
            {
                ConfigManager.SaveProcessedFiles(processedFilesPath, processedFiles);
                if (DebugLogs)
                    Console.WriteLine($"  [DEBUG] Saved {processedFiles.Count} processed files to {processedFilesPath}");
            }

            return processedCount;
        }

        public static ProcessResult ProcessFile(string filePath, string sourceDir, string destDir, HashSet<string> processedFiles)
        {
            string fullFilePath = Path.GetFullPath(filePath);

 
            if (processedFiles.Contains(fullFilePath))
            {
                if (DebugLogs)
                    Console.WriteLine($"  [DEBUG] Already processed: {fullFilePath}");
                return new ProcessResult { WasProcessed = false, ShouldSkipInFuture = false };
            }

            if (IsFileInUse(filePath) || IsFileGrowing(filePath))
            {
                if (DebugLogs)
                    Console.WriteLine($"  [DEBUG] File in use or growing, skipping: {fullFilePath}");
                return new ProcessResult { WasProcessed = false, ShouldSkipInFuture = false };
            }

            try
            {
                string relativePath = GetRelativePathWithoutUri(filePath, sourceDir);
                string destPath = Path.Combine(destDir, relativePath);

                string fileName = Path.GetFileName(destPath);
                string? directory = Path.GetDirectoryName(destPath);

                if (string.IsNullOrEmpty(directory))
                {
                    if (DebugLogs)
                        Console.WriteLine($"  [DEBUG] Invalid directory path: {destPath}");
                    return new ProcessResult { WasProcessed = false, ShouldSkipInFuture = false };
                }

                string compressedDestPath = Path.Combine(directory, fileName + ".bz2");

                if (DebugLogs)
                {
                    Console.WriteLine($"  [DEBUG] Processing: {fullFilePath}");
                    Console.WriteLine($"  [DEBUG] Relative path: {relativePath}");
                    Console.WriteLine($"  [DEBUG] Destination: {compressedDestPath}");
                }

                Directory.CreateDirectory(directory);

                if (File.Exists(compressedDestPath))
                {
                    if (DebugLogs)
                        Console.WriteLine($"  [DEBUG] Compressed file already exists: {compressedDestPath}");

                    processedFiles.Add(fullFilePath);
                    return new ProcessResult { WasProcessed = false, ShouldSkipInFuture = true };
                }

                if (DebugLogs)
                    Console.WriteLine($"  [DEBUG] Compressing: {fullFilePath} -> {compressedDestPath}");

                if (CompressionManager.CompressFile(filePath, compressedDestPath))
                {
                    if (DebugLogs)
                        Console.WriteLine($"  [DEBUG] Successfully compressed: {fullFilePath} -> {compressedDestPath}");

                    SetLinuxPermissions(compressedDestPath);

                    processedFiles.Add(fullFilePath);
                    return new ProcessResult { WasProcessed = true, ShouldSkipInFuture = true };
                }
                else
                {
                    if (DebugLogs)
                        Console.WriteLine($"  [DEBUG] Compression failed: {fullFilePath}");

                    return new ProcessResult { WasProcessed = false, ShouldSkipInFuture = false };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing file {fullFilePath}: {ex.Message}");
                return new ProcessResult { WasProcessed = false, ShouldSkipInFuture = false };
            }
        }

        private static string GetRelativePathWithoutUri(string fullPath, string basePath)
        {
            fullPath = Path.GetFullPath(fullPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            basePath = Path.GetFullPath(basePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (!fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath;
            }

            return fullPath.Substring(basePath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static void SetLinuxPermissions(string filePath)
        {
            try
            {
                if (Environment.OSVersion.Platform == PlatformID.Unix ||
                    Environment.OSVersion.Platform == PlatformID.MacOSX)
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
    }

    public class ProcessResult
    {
        public bool WasProcessed { get; set; }
        public bool ShouldSkipInFuture { get; set; }
    }
}