using System;
using System.Diagnostics;
using System.Reflection;

namespace SourceEngineFastDownloadTool
{
    public static class ApplicationInfo
    {
        public static string Name => "Source Engine FastDownload Tool";
        public static string Version => GetAssemblyVersion();
        public static string Developer => "SyntX";
        public static string Company => "SyntX";
        public static string Copyright => "Copyright © 2025 SyntX";
        public static string Description => "FastDL file compression tool for game server content";
        public static string Website => "https://github.com/SyntX34";
        public static string SteamProfile => "https://steamcommunity.com/id/SyntX34";
        public static string DiscordName => "nh_syntx (SyntX#0164)";
        public static string DiscordServer => "https://discord.gg/2DjsQ4xdd5";

        public static void DisplayApplicationInfo()
        {
            Console.Clear();
            Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                 SOURCE ENGINE FASTDOWNLOAD TOOL                              ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║ Version: {Version,-65} ║");
            Console.WriteLine($"║ Developer: {Developer,-62} ║");
            Console.WriteLine($"║ Company: {Company,-63} ║");
            Console.WriteLine($"║ {Copyright,-70} ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║ Description: {Description,-56} ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║                              WEBSITES                                        ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║ GitHub:    {Website,-60} ║");
            Console.WriteLine($"║ Steam:     {SteamProfile,-60} ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║                            SUPPORT & CONTACT                                 ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║ Discord:   {DiscordName,-60} ║");
            Console.WriteLine($"║ Server:    {DiscordServer,-60} ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
        }

        public static void DisplayVersion()
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine($"║ {Name} v{Version,-45} ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║ {Copyright,-70} ║");
            Console.WriteLine($"║ Developed by: {Developer,-57} ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
        }

        public static void DisplayHelp()
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                         USAGE GUIDE                                          ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ Usage: Source Engine FastDownload Tool [options]                             ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ OPTIONS:                                                                     ║");
            Console.WriteLine("║   --help, -h       Show this help message                                    ║");
            Console.WriteLine("║   --version, -v    Show version information                                  ║");
            Console.WriteLine("║   --config <path>  Use custom config file                                    ║");
            Console.WriteLine("║   --once           Process once and exit (no continuous monitoring)          ║");
            Console.WriteLine("║   --24x7           Enable 24x7 continuous monitoring                         ║");
            Console.WriteLine("║   --debug          Enable debug logging (shows file details)                 ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ EXAMPLES:                                                                    ║");
            Console.WriteLine("║   Source Engine FastDownload Tool                 # Normal operation         ║");
            Console.WriteLine("║   Source Engine FastDownload Tool --once          # Process once and exit    ║");
            Console.WriteLine("║   Source Engine FastDownload Tool --debug         # Enable debug logging     ║");
            Console.WriteLine("║   Source Engine FastDownload Tool --config myconfig.json                     ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
        }

        public static void DisplayCompressionInfo(string tool, bool sevenZip, bool winRar, bool dotNet, string toolPath)
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                         COMPRESSION INFORMATION                             ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║ Current Tool: {tool,-56} ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║ 7-Zip Available: {sevenZip,-52} ║");
            Console.WriteLine($"║ WinRAR Available: {winRar,-52} ║");
            Console.WriteLine($"║ .NET GZip Available: {dotNet,-49} ║");

            if (!string.IsNullOrEmpty(toolPath))
            {
                Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
                Console.WriteLine($"║ Tool Path: {toolPath,-59} ║");
            }

            Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
        }

        public static void DisplayServerInfo(int serverCount, int fileTypeCount, int interval, bool run24x7, bool debug)
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                         CONFIGURATION SUMMARY                               ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║ Servers: {serverCount,-61} ║");
            Console.WriteLine($"║ File Types: {fileTypeCount,-58} ║");
            Console.WriteLine($"║ Check Interval: {interval} seconds{"",-50} ║");
            Console.WriteLine($"║ 24x7 Mode: {run24x7,-59} ║");
            Console.WriteLine($"║ Debug Logs: {debug,-59} ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
        }

        public static void DisplayProcessingHeader(string serverName, string source, string destination)
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine($"║ PROCESSING: {serverName,-58} ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║ Source: {TruncatePath(source, 65),-65} ║");
            Console.WriteLine($"║ Destination: {TruncatePath(destination, 62),-62} ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
        }

        public static void DisplayProcessingResult(string serverName, int processed, double duration)
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine($"║ RESULT: {serverName,-61} ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║ Files Processed: {processed,-52} ║");
            Console.WriteLine($"║ Time Taken: {duration:F1} seconds{"",-50} ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
        }

        private static string GetAssemblyVersion()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var versionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
                return versionInfo.FileVersion ?? "1.0.0.0";
            }
            catch
            {
                return "1.0.0.0";
            }
        }

        private static string TruncatePath(string path, int maxLength)
        {
            if (path.Length <= maxLength)
                return path;

            // Keep the beginning and end of the path
            int partLength = (maxLength - 3) / 2;
            return path.Substring(0, partLength) + "..." + path.Substring(path.Length - partLength);
        }
    }
}