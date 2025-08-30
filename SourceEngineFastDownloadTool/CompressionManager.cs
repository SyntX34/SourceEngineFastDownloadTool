using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SourceEngineFastDownloadTool
{
    public enum CompressionTool
    {
        SevenZip,
        WinRAR,
        DotNetZip,
        None
    }

    public static class CompressionManager
    {
        public static CompressionTool CurrentTool { get; private set; } = CompressionTool.None;

        public static bool Initialize()
        {
            CurrentTool = DetectAvailableTool();
            return CurrentTool != CompressionTool.None;
        }

        public static CompressionTool DetectAvailableTool()
        {
            if (IsToolAvailable(CompressionTool.SevenZip)) return CompressionTool.SevenZip;
            if (IsToolAvailable(CompressionTool.WinRAR)) return CompressionTool.WinRAR;
            if (IsToolAvailable(CompressionTool.DotNetZip)) return CompressionTool.DotNetZip;

            return CompressionTool.None;
        }

        public static bool IsToolAvailable(CompressionTool tool)
        {
            try
            {
                switch (tool)
                {
                    case CompressionTool.SevenZip:
                        return CheckToolAvailable("7z.exe") || CheckToolAvailable("7z") ||
                               File.Exists(@"C:\Program Files\7-Zip\7z.exe");

                    case CompressionTool.WinRAR:
                        return CheckToolAvailable("winrar.exe") ||
                               File.Exists(@"C:\Program Files\WinRAR\WinRAR.exe");

                    case CompressionTool.DotNetZip:
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

        private static bool CheckToolAvailable(string toolName)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "where",
                        Arguments = toolName,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return !string.IsNullOrEmpty(output) && output.Contains(toolName);
            }
            catch
            {
                return false;
            }
        }

        private static bool CompressWithBzip2Linux(string sourcePath, string destPath)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "bzip2",
                        Arguments = $"-c -1 \"{sourcePath}\" > \"{destPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    }
                };

                process.Start();
                process.WaitForExit();
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        public static bool CompressFile(string sourcePath, string destPath)
        {
            try
            {
                if (PlatformUtils.IsLinux || PlatformUtils.IsOSX)
                {
                    return CompressWithBzip2Linux(sourcePath, destPath);
                }

                switch (CurrentTool)
                {
                    case CompressionTool.SevenZip:
                        return CompressWithSevenZipBzip2(sourcePath, destPath);

                    case CompressionTool.WinRAR:
                        return CompressWithWinRAR(sourcePath, destPath);

                    case CompressionTool.DotNetZip:
                        return CompressWithDotNet(sourcePath, destPath);

                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Compression failed: {ex.Message}");
                return false;
            }
        }

        private static bool CompressWithSevenZipBzip2(string sourcePath, string destPath)
        {
            try
            {
                string sevenZipPath = FindSevenZipPath();
                if (string.IsNullOrEmpty(sevenZipPath)) return false;

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = sevenZipPath,
                        Arguments = $"a -tbzip2 -mx=1 \"{destPath}\" \"{sourcePath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.WaitForExit();
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool CompressWithWinRAR(string sourcePath, string destPath)
        {
            try
            {
                string winRarPath = FindWinRARPath();
                if (string.IsNullOrEmpty(winRarPath)) return false;
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = winRarPath,
                        Arguments = $"a -m1 -ep \"{destPath}\" \"{sourcePath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool CompressWithDotNet(string sourcePath, string destPath)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destPath));

                using (var inputStream = File.OpenRead(sourcePath))
                using (var outputStream = File.Create(destPath))
                using (var compressionStream = new System.IO.Compression.GZipStream(outputStream, System.IO.Compression.CompressionLevel.Optimal))
                {
                    inputStream.CopyTo(compressionStream);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DotNet compression failed: {ex.Message}");
                return false;
            }
        }

        private static string FindSevenZipPath()
        {
            var paths = new[]
            {
                @"C:\Program Files\7-Zip\7z.exe",
                @"C:\Program Files (x86)\7-Zip\7z.exe",
                "7z.exe",
                "7z"
            };

            return paths.FirstOrDefault(File.Exists);
        }

        private static string FindWinRARPath()
        {
            var paths = new[]
            {
                @"C:\Program Files\WinRAR\WinRAR.exe",
                @"C:\Program Files (x86)\WinRAR\WinRAR.exe",
                "winrar.exe"
            };

            return paths.FirstOrDefault(File.Exists);
        }

        public static void DisplayCompressionInfoFormatted()
        {
            bool sevenZip = IsToolAvailable(CompressionTool.SevenZip);
            bool winRar = IsToolAvailable(CompressionTool.WinRAR);
            bool dotNet = IsToolAvailable(CompressionTool.DotNetZip);

            string toolPath = "";
            if (CurrentTool == CompressionTool.SevenZip)
                toolPath = FindSevenZipPath();
            else if (CurrentTool == CompressionTool.WinRAR)
                toolPath = FindWinRARPath();

            ApplicationInfo.DisplayCompressionInfo(CurrentTool.ToString(), sevenZip, winRar, dotNet, toolPath);
        }
    }
}