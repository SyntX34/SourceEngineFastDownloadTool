using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;

namespace SourceEngineFastDownloadTool
{
    public enum CompressionTool
    {
        SevenZip,
        Bzip2,
        SharpCompressBzip2,
        DotNetGZip,
        None
    }

    public static class CompressionManager
    {
        public static CompressionTool CurrentTool { get; private set; } = CompressionTool.None;
        private static readonly ConcurrentDictionary<CompressionTool, bool> _toolAvailabilityCache = new();
        private static readonly ConcurrentDictionary<string, bool> _commandCache = new();
        private static string? _cachedSevenZipPath;
        private static string? _cachedBzip2Path;
        private const int BUFFER_SIZE = 65536;

        private static readonly ThreadLocal<byte[]> _bufferPool = new(() => new byte[BUFFER_SIZE]);

        public static bool Initialize()
        {
            CurrentTool = DetectAvailableTool();
            return CurrentTool != CompressionTool.None;
        }

        public static CompressionTool DetectAvailableTool()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (IsToolAvailable(CompressionTool.SevenZip))
                    return CompressionTool.SevenZip;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (IsToolAvailable(CompressionTool.Bzip2))
                    return CompressionTool.Bzip2;
            }

            return CompressionTool.SharpCompressBzip2;
        }

        public static bool IsToolAvailable(CompressionTool tool)
        {
            if (_toolAvailabilityCache.TryGetValue(tool, out bool cachedResult))
                return cachedResult;

            bool isAvailable = CheckToolAvailabilityInternal(tool);
            _toolAvailabilityCache[tool] = isAvailable;
            return isAvailable;
        }

        private static bool CheckToolAvailabilityInternal(CompressionTool tool)
        {
            try
            {
                switch (tool)
                {
                    case CompressionTool.SevenZip:
                        return CheckSevenZipAvailable();

                    case CompressionTool.Bzip2:
                        return CheckBzip2Available();

                    case CompressionTool.SharpCompressBzip2:
                    case CompressionTool.DotNetGZip:
                        return true;

                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool CheckSevenZipAvailable()
        {
            if (_cachedSevenZipPath != null)
                return true;

            var paths = new[]
            {
                @"C:\Program Files\7-Zip\7z.exe",
                @"C:\Program Files (x86)\7-Zip\7z.exe",
                "7z.exe",
                "7z"
            };

            foreach (var path in paths)
            {
                if (File.Exists(path) || CheckToolAvailableInPath(path))
                {
                    _cachedSevenZipPath = path;
                    return true;
                }
            }

            return false;
        }

        private static bool CheckBzip2Available()
        {
            if (_cachedBzip2Path != null)
                return true;

            if (CheckToolAvailableInPath("bzip2"))
            {
                _cachedBzip2Path = "bzip2";
                return true;
            }

            return false;
        }

        private static bool CheckToolAvailableInPath(string toolName)
        {
            if (_commandCache.TryGetValue(toolName, out bool cachedResult))
                return cachedResult;

            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which",
                        Arguments = toolName,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WorkingDirectory = Environment.CurrentDirectory
                    }
                };

                process.Start();

                bool finished = process.WaitForExit(2000);
                if (!finished)
                {
                    try { process.Kill(); } catch { }
                    _commandCache[toolName] = false;
                    return false;
                }

                string output = process.StandardOutput.ReadToEnd();
                bool isAvailable = process.ExitCode == 0 && !string.IsNullOrEmpty(output.Trim());

                _commandCache[toolName] = isAvailable;
                return isAvailable;
            }
            catch
            {
                _commandCache[toolName] = false;
                return false;
            }
        }

        public static bool CompressFile(string sourcePath, string destPath)
        {
            try
            {
                if (!File.Exists(sourcePath))
                    return false;

                string? directory = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (File.Exists(destPath))
                {
                    try { File.Delete(destPath); } catch { }
                }

                switch (CurrentTool)
                {
                    case CompressionTool.SevenZip:
                        return CompressWithSevenZipBzip2(sourcePath, destPath);

                    case CompressionTool.Bzip2:
                        return CompressWithBzip2Native(sourcePath, destPath);

                    case CompressionTool.SharpCompressBzip2:
                        return CompressWithSharpCompressBzip2Optimized(sourcePath, destPath);

                    case CompressionTool.DotNetGZip:
                        return CompressWithDotNetGZipOptimized(sourcePath, destPath);

                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                if (FileProcessor.DebugLogs)
                    Console.WriteLine($"Compression failed for {sourcePath}: {ex.Message}");
                return false;
            }
        }

        private static bool CompressWithSevenZipBzip2(string sourcePath, string destPath)
        {
            try
            {
                string sevenZipPath = _cachedSevenZipPath ?? FindSevenZipPath();
                if (string.IsNullOrEmpty(sevenZipPath)) return false;

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = sevenZipPath,
                        Arguments = $"a -tbzip2 -mx=1 -ms=off -mmt=on \"{destPath}\" \"{sourcePath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    }
                };

                process.Start();

                bool finished = process.WaitForExit(30000);
                if (!finished)
                {
                    try { process.Kill(); } catch { }
                    return false;
                }

                return process.ExitCode == 0 && File.Exists(destPath);
            }
            catch
            {
                return false;
            }
        }

        private static bool CompressWithBzip2Native(string sourcePath, string destPath)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-c \"bzip2 -c -1 '{sourcePath}' > '{destPath}'\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true
                    }
                };

                process.Start();
                bool finished = process.WaitForExit(30000);
                if (!finished)
                {
                    try { process.Kill(); } catch { }
                    return false;
                }

                return process.ExitCode == 0 && File.Exists(destPath);
            }
            catch (Exception ex)
            {
                if (FileProcessor.DebugLogs)
                    Console.WriteLine($"Bzip2 compression failed: {ex.Message}");
                return false;
            }
        }

        private static bool CompressWithSharpCompressBzip2Optimized(string sourcePath, string destPath)
        {
            try
            {
                var fileInfo = new FileInfo(sourcePath);

                using (var inputStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, BUFFER_SIZE, FileOptions.SequentialScan))
                using (var outputStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, BUFFER_SIZE, FileOptions.WriteThrough))
                using (var bzip2Stream = new BZip2Stream(outputStream, CompressionMode.Compress, true))
                {
                    var buffer = _bufferPool.Value;
                    int bytesRead;

                    while ((bytesRead = inputStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        bzip2Stream.Write(buffer, 0, bytesRead);
                    }

                    bzip2Stream.Flush();
                }

                return File.Exists(destPath) && new FileInfo(destPath).Length > 0;
            }
            catch (Exception ex)
            {
                if (FileProcessor.DebugLogs)
                    Console.WriteLine($"SharpCompress Bzip2 compression failed: {ex.Message}");

                try { if (File.Exists(destPath)) File.Delete(destPath); } catch { }
                return false;
            }
        }

        private static bool CompressWithDotNetGZipOptimized(string sourcePath, string destPath)
        {
            try
            {
                using (var inputStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, BUFFER_SIZE, FileOptions.SequentialScan))
                using (var outputStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, BUFFER_SIZE, FileOptions.WriteThrough))
                using (var compressionStream = new System.IO.Compression.GZipStream(outputStream, System.IO.Compression.CompressionLevel.Fastest))
                {
                    var buffer = _bufferPool.Value;
                    int bytesRead;

                    while ((bytesRead = inputStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        compressionStream.Write(buffer, 0, bytesRead);
                    }

                    compressionStream.Flush();
                }

                return File.Exists(destPath) && new FileInfo(destPath).Length > 0;
            }
            catch (Exception ex)
            {
                if (FileProcessor.DebugLogs)
                    Console.WriteLine($"DotNet GZip compression failed: {ex.Message}");

                try { if (File.Exists(destPath)) File.Delete(destPath); } catch { }
                return false;
            }
        }

        private static string? FindSevenZipPath()
        {
            if (_cachedSevenZipPath != null)
                return _cachedSevenZipPath;

            var paths = new[]
            {
                @"C:\Program Files\7-Zip\7z.exe",
                @"C:\Program Files (x86)\7-Zip\7z.exe",
                "7z.exe",
                "7z"
            };

            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    _cachedSevenZipPath = path;
                    return path;
                }
            }

            return null;
        }

        public static void DisplayCompressionInfoFormatted()
        {
            bool sevenZip = IsToolAvailable(CompressionTool.SevenZip);
            bool bzip2 = IsToolAvailable(CompressionTool.Bzip2);

            string toolPath = "";
            if (CurrentTool == CompressionTool.SevenZip)
                toolPath = _cachedSevenZipPath ?? FindSevenZipPath() ?? "Not found";
            else if (CurrentTool == CompressionTool.Bzip2)
                toolPath = _cachedBzip2Path ?? "bzip2 (system)";

            Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                         COMPRESSION INFORMATION                             ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║ Current Tool: {CurrentTool,-54} ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║ 7-Zip Available: {sevenZip,-52} ║");
            Console.WriteLine($"║ Bzip2 Available: {bzip2,-53} ║");

            if (!string.IsNullOrEmpty(toolPath))
            {
                Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
                Console.WriteLine($"║ Tool Path: {toolPath,-59} ║");
            }

            Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║ Optimizations: Large buffers, cached paths, fast compression            ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
        }
        public static void Cleanup()
        {
            _bufferPool?.Dispose();
        }
    }
}