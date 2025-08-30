using System;
using System.Runtime.InteropServices;

namespace SourceEngineFastDownloadTool
{
    public static class PlatformUtils
    {
        public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        public static bool IsOSX => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        public static string GetCompressionTool()
        {
            if (IsWindows)
            {
                return "7z.exe";
            }
            else if (IsLinux || IsOSX)
            {
                return "bzip2";
            }
            return "compression";
        }

        public static string FormatPath(string path)
        {
            if (IsWindows)
            {
                return path.Replace("/", "\\");
            }
            else
            {
                return path.Replace("\\", "/");
            }
        }
    }
}