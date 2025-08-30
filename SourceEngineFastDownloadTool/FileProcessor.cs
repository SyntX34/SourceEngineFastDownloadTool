using System;
using System.Collections.Generic;
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

        public static int ProcessFiles(string sourceDir, string destDir, HashSet<string> processedFiles, HashSet<string> extensions)
        {
            int processedCount = 0;
            var files = GetAllFiles(sourceDir, extensions);

            if (DebugLogs)
                Console.WriteLine($"  [DEBUG] Processing {files.Count} files from {sourceDir}");

            foreach (var file in files)
            {
                if (ProcessFile(file, sourceDir, destDir, processedFiles))
                {
                    processedCount++;

                    if (processedCount % 10 == 0)
                    {
                        Console.WriteLine($"Processed {processedCount} files...");
                    }
                }
            }

            return processedCount;
        }

        public static bool ProcessFile(string filePath, string sourceDir, string destDir, HashSet<string> processedFiles)
        {
            if (processedFiles.Contains(filePath))
            {
                if (DebugLogs)
                    Console.WriteLine($"  [DEBUG] Already processed: {filePath}");
                return false;
            }

            if (IsFileInUse(filePath) || IsFileGrowing(filePath))
            {
                return false;
            }

            try
            {
                string relativePath = GetRelativePath(filePath, sourceDir);
                string destPath = Path.Combine(destDir, relativePath);
                string fileName = Path.GetFileName(destPath);
                string directory = Path.GetDirectoryName(destPath);
                destPath = Path.Combine(directory, fileName + ".bz2");

                if (DebugLogs)
                {
                    Console.WriteLine($"  [DEBUG] Relative path: {relativePath}");
                    Console.WriteLine($"  [DEBUG] Destination: {destPath}");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destPath));

                if (File.Exists(destPath))
                {
                    if (DebugLogs)
                        Console.WriteLine($"  [DEBUG] Destination file already exists: {destPath}");

                    processedFiles.Add(filePath);
                    return false;
                }

                if (DebugLogs)
                    Console.WriteLine($"  [DEBUG] Compressing: {filePath} -> {destPath}");

                if (CompressionManager.CompressFile(filePath, destPath))
                {
                    if (DebugLogs)
                        Console.WriteLine($"  [DEBUG] Successfully compressed: {filePath} -> {destPath}");

                    processedFiles.Add(filePath);
                    return true;
                }
                else
                {
                    if (DebugLogs)
                        Console.WriteLine($"  [DEBUG] Compression failed: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing file {filePath}: {ex.Message}");
            }

            return false;
        }

        private static string GetRelativePath(string fullPath, string basePath)
        {
            Uri pathUri = new Uri(fullPath);
            Uri baseUri = new Uri(basePath + Path.DirectorySeparatorChar);
            return Uri.UnescapeDataString(baseUri.MakeRelativeUri(pathUri).ToString());
        }
    }
}