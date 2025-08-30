using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SourceEngineFastDownloadTool
{
    public class ServerConfig
    {
        public string Name { get; set; } = "";
        public string Source { get; set; } = "";
        public string Destination { get; set; } = "";
    }

    public class AppConfig
    {
        public int CheckInterval { get; set; } = 120;
        public string FileTypes { get; set; } = "mp3,vtx,bsp,nav,mdl,phy,vmt,vtf,dx80.vtx,dx90.vtx,sw.vtx,wav";
        public string ProcessedFilesPath { get; set; } = "processed_files.txt";
        public bool Run24x7 { get; set; } = true;
        public bool DebugLogs { get; set; } = false;
        public List<ServerConfig> Servers { get; set; } = new List<ServerConfig>();
    }

    public static class ConfigManager
    {
        public static AppConfig LoadConfig(string configPath)
        {
            try
            {
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    var config = JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
                    
                    foreach (var server in config.Servers)
                    {
                        server.Source = NormalizePath(server.Source);
                        server.Destination = NormalizePath(server.Destination);
                    }
                    
                    return config;
                }
                else
                {
                    Console.WriteLine($"Config file not found: {configPath}");
                    Console.WriteLine("Creating default config...");
                    var defaultConfig = CreateDefaultConfig();
                    SaveConfig(configPath, defaultConfig);
                    return defaultConfig;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading config: {ex.Message}");
                return CreateDefaultConfig();
            }
        }

        private static AppConfig CreateDefaultConfig()
        {
            return new AppConfig
            {
                CheckInterval = 120,
                FileTypes = "mp3,vtx,bsp,nav,mdl,phy,vmt,vtf,dx80.vtx,dx90.vtx,sw.vtx,wav",
                ProcessedFilesPath = "processed_files.txt",
                Run24x7 = true,
                DebugLogs = false,
                Servers = new List<ServerConfig>
                {
                    new ServerConfig
                    {
                        Name = "Example Server",
                        Source = PlatformUtils.IsWindows ? @"C:\GameServer\cstrike" : "/home/user/gameserver/cstrike",
                        Destination = PlatformUtils.IsWindows ? @"C:\FastDL\cstrike" : "/var/www/html/fastdl/cstrike"
                    }
                }
            };
        }

        public static bool SaveConfig(string configPath, AppConfig config)
        {
            try
            {
                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(configPath, json);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving config: {ex.Message}");
                return false;
            }
        }

        public static HashSet<string> LoadFileTypes(string fileTypesConfig)
        {
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (!string.IsNullOrEmpty(fileTypesConfig))
                {
                    var types = fileTypesConfig.Split(',')
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrEmpty(t))
                        .Select(t => t.StartsWith(".") ? t : "." + t);

                    foreach (var type in types)
                    {
                        extensions.Add(type);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading file types: {ex.Message}");
            }

            if (extensions.Count == 0)
            {
                extensions.UnionWith(new[] { ".nav", ".bsp", ".mdl", ".phy", ".vvd", ".vtf", ".vmt", ".wav", ".mp3", ".vtx" });
            }

            return extensions;
        }

        public static HashSet<string> LoadProcessedFiles(string filePath)
        {
            var processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (File.Exists(filePath))
                {
                    foreach (string line in File.ReadLines(filePath))
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            string normalizedPath = NormalizePath(line.Trim());
                            processedFiles.Add(normalizedPath);
                        }
                    }
                    
                    Console.WriteLine($"Loaded {processedFiles.Count} processed files from {filePath}");
                }
                else
                {
                    Console.WriteLine($"Processed files list not found: {filePath}");
                    Console.WriteLine("Creating new processed files list...");
                    File.WriteAllText(filePath, "");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading processed files: {ex.Message}");
            }

            return processedFiles;
        }

        public static bool SaveProcessedFiles(string filePath, HashSet<string> processedFiles)
        {
            try
            {
                string backupPath = filePath + ".backup";
                if (File.Exists(filePath))
                {
                    File.Copy(filePath, backupPath, true);
                }

                var sortedFiles = processedFiles.OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
                
                string tempPath = filePath + ".tmp";
                File.WriteAllLines(tempPath, sortedFiles);
                
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                File.Move(tempPath, filePath);

                if (File.Exists(backupPath))
                {
                    try { File.Delete(backupPath); } catch { }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving processed files: {ex.Message}");
                
                string backupPath = filePath + ".backup";
                if (File.Exists(backupPath) && !File.Exists(filePath))
                {
                    try
                    {
                        File.Copy(backupPath, filePath, true);
                        Console.WriteLine("Restored processed files from backup.");
                    }
                    catch (Exception backupEx)
                    {
                        Console.WriteLine($"Failed to restore backup: {backupEx.Message}");
                    }
                }

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
    }
}