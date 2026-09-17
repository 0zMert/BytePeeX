using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Folderize.Models;

namespace Folderize.Services
{
    public class InstalledAppService
    {
        public List<InstalledAppModel> GetInstalledApplications()
        {
            var apps = new Dictionary<string, InstalledAppModel>(StringComparer.OrdinalIgnoreCase);

            // 1. Build UserAssist index of last executed applications
            var userAssistRuns = LoadUserAssistHistory();

            // 2. Scan standard Windows Uninstall registries (64-bit, 32-bit HKLM, HKCU)
            ScanRegistryKey(RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64),
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", apps, userAssistRuns);

            ScanRegistryKey(RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32),
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", apps, userAssistRuns);

            ScanRegistryKey(RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default),
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", apps, userAssistRuns);

            // 3. Scan Steam Games (appmanifest_*.acf files have exact SizeOnDisk and LastPlayed timestamps)
            ScanSteamGames(apps);

            // 4. Scan Epic Games manifests
            ScanEpicGames(apps, userAssistRuns);

            // 5. Scan EA Games / Origin manifests (Battlefield 1, FIFA, etc.)
            ScanEaAndOriginGames(apps, userAssistRuns);

            // Calculate percentage of total installed space
            var list = apps.Values
                .Where(a => !string.IsNullOrWhiteSpace(a.Name) && a.SizeBytes > 0)
                .OrderByDescending(a => a.SizeBytes)
                .ToList();

            long total = list.Sum(a => a.SizeBytes);
            if (total > 0)
            {
                foreach (var a in list)
                {
                    a.PercentOfTotal = (double)a.SizeBytes / total * 100.0;
                }
            }

            return list;
        }

        private static void ScanRegistryKey(
            RegistryKey baseKey,
            string subKeyPath,
            Dictionary<string, InstalledAppModel> apps,
            Dictionary<string, DateTime> userAssistRuns)
        {
            try
            {
                using var key = baseKey.OpenSubKey(subKeyPath);
                if (key == null) return;

                foreach (var subName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var subKey = key.OpenSubKey(subName);
                        if (subKey == null) continue;

                        var systemComponent = subKey.GetValue("SystemComponent");
                        if (systemComponent is int sc && sc == 1) continue;

                        var parentKeyName = subKey.GetValue("ParentKeyName") as string;
                        if (!string.IsNullOrEmpty(parentKeyName)) continue;

                        var displayName = subKey.GetValue("DisplayName") as string;
                        if (string.IsNullOrWhiteSpace(displayName)) continue;

                        displayName = displayName.Trim();
                        if (apps.ContainsKey(displayName)) continue;

                        long sizeBytes = 0;
                        var estimatedSize = subKey.GetValue("EstimatedSize");
                        if (estimatedSize is int sizeInt && sizeInt > 0)
                        {
                            sizeBytes = (long)sizeInt * 1024;
                        }
                        else if (estimatedSize is long sizeLong && sizeLong > 0)
                        {
                            sizeBytes = sizeLong * 1024;
                        }

                        var installLocation = subKey.GetValue("InstallLocation") as string ?? "";
                        var displayIcon = subKey.GetValue("DisplayIcon") as string ?? "";
                        var uninstallString = subKey.GetValue("UninstallString") as string ?? "";

                        // Determine actual installation directory
                        string realFolder = installLocation;
                        if (string.IsNullOrWhiteSpace(realFolder) || !Directory.Exists(realFolder))
                        {
                            realFolder = ExtractFolderFromPath(displayIcon);
                            if (string.IsNullOrWhiteSpace(realFolder) || !Directory.Exists(realFolder))
                            {
                                realFolder = ExtractFolderFromPath(uninstallString);
                            }
                        }

                        // Always verify against actual folder size for high accuracy (e.g. 40GB games reporting 100MB)
                        if (!string.IsNullOrWhiteSpace(realFolder) && Directory.Exists(realFolder))
                        {
                            try
                            {
                                long actualFolderSize = CalculateDirectorySize(realFolder);
                                if (actualFolderSize > sizeBytes)
                                {
                                    sizeBytes = actualFolderSize;
                                }
                            }
                            catch
                            {
                            }
                        }

                        if (sizeBytes <= 0) continue;

                        var publisher = subKey.GetValue("Publisher") as string ?? "";
                        var displayVersion = subKey.GetValue("DisplayVersion") as string ?? "";

                        // Determine last execution time
                        DateTime? lastRun = DetermineLastRun(displayName, realFolder, displayIcon, publisher, userAssistRuns);

                        apps[displayName] = new InstalledAppModel
                        {
                            Name = displayName,
                            Publisher = publisher,
                            Version = displayVersion,
                            SizeBytes = sizeBytes,
                            InstallLocation = realFolder,
                            LastRunTime = lastRun
                        };
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        private static void ScanSteamGames(Dictionary<string, InstalledAppModel> apps)
        {
            try
            {
                using var steamKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (steamKey == null) return;

                string? steamPath = steamKey.GetValue("SteamPath") as string;
                if (string.IsNullOrEmpty(steamPath) || !Directory.Exists(steamPath)) return;

                var libraryDirs = new List<string> { steamPath };

                // Parse libraryfolders.vdf to find all Steam library drives
                string vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                if (File.Exists(vdfPath))
                {
                    try
                    {
                        var lines = File.ReadAllLines(vdfPath);
                        foreach (var line in lines)
                        {
                            var match = Regex.Match(line, "\"path\"\\s+\"([^\"]+)\"");
                            if (match.Success)
                            {
                                string p = match.Groups[1].Value.Replace("\\\\", "\\");
                                if (Directory.Exists(p) && !libraryDirs.Contains(p, StringComparer.OrdinalIgnoreCase))
                                {
                                    libraryDirs.Add(p);
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                // In each library, scan appmanifest_*.acf
                foreach (var lib in libraryDirs)
                {
                    string steamapps = Path.Combine(lib, "steamapps");
                    if (!Directory.Exists(steamapps)) continue;

                    foreach (var manifestFile in Directory.EnumerateFiles(steamapps, "appmanifest_*.acf"))
                    {
                        try
                        {
                            var content = File.ReadAllText(manifestFile);
                            var nameMatch = Regex.Match(content, "\"name\"\\s+\"([^\"]+)\"");
                            var sizeMatch = Regex.Match(content, "\"SizeOnDisk\"\\s+\"([^\"]+)\"");
                            var installMatch = Regex.Match(content, "\"installdir\"\\s+\"([^\"]+)\"");
                            var lastPlayedMatch = Regex.Match(content, "\"LastPlayed\"\\s+\"([^\"]+)\"");

                            if (!nameMatch.Success) continue;
                            string gameName = nameMatch.Groups[1].Value.Trim();

                            long sizeOnDisk = 0;
                            if (sizeMatch.Success && long.TryParse(sizeMatch.Groups[1].Value, out long parsedSize))
                            {
                                sizeOnDisk = parsedSize;
                            }

                            string installDir = installMatch.Success ? Path.Combine(steamapps, "common", installMatch.Groups[1].Value) : "";

                            if (sizeOnDisk <= 0 && !string.IsNullOrEmpty(installDir) && Directory.Exists(installDir))
                            {
                                sizeOnDisk = CalculateDirectorySize(installDir);
                            }

                            DateTime? lastRun = null;
                            if (lastPlayedMatch.Success && long.TryParse(lastPlayedMatch.Groups[1].Value, out long lastPlayedUnix) && lastPlayedUnix > 0)
                            {
                                lastRun = DateTimeOffset.FromUnixTimeSeconds(lastPlayedUnix).LocalDateTime;
                            }

                            if (sizeOnDisk > 0)
                            {
                                if (apps.TryGetValue(gameName, out var existing))
                                {
                                    existing.SizeBytes = Math.Max(existing.SizeBytes, sizeOnDisk);
                                    if (lastRun.HasValue) existing.LastRunTime = lastRun;
                                    if (string.IsNullOrEmpty(existing.InstallLocation)) existing.InstallLocation = installDir;
                                }
                                else
                                {
                                    apps[gameName] = new InstalledAppModel
                                    {
                                        Name = gameName,
                                        Publisher = "Valve / Steam",
                                        Version = "Steam Game",
                                        SizeBytes = sizeOnDisk,
                                        InstallLocation = installDir,
                                        LastRunTime = lastRun
                                    };
                                }
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static void ScanEpicGames(Dictionary<string, InstalledAppModel> apps, Dictionary<string, DateTime> userAssistRuns)
        {
            try
            {
                string manifestsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Epic\EpicGamesLauncher\Data\Manifests");
                if (!Directory.Exists(manifestsPath)) return;

                foreach (var itemFile in Directory.EnumerateFiles(manifestsPath, "*.item"))
                {
                    try
                    {
                        var content = File.ReadAllText(itemFile);
                        var nameMatch = Regex.Match(content, "\"DisplayName\"\\s*:\\s*\"([^\"]+)\"");
                        var locMatch = Regex.Match(content, "\"InstallLocation\"\\s*:\\s*\"([^\"]+)\"");
                        var sizeMatch = Regex.Match(content, "\"InstalledSizeBytes\"\\s*:\\s*(\\d+)");

                        if (!nameMatch.Success) continue;
                        string gameName = nameMatch.Groups[1].Value.Trim();

                        long sizeBytes = 0;
                        if (sizeMatch.Success && long.TryParse(sizeMatch.Groups[1].Value, out long sz))
                        {
                            sizeBytes = sz;
                        }

                        string installLoc = locMatch.Success ? locMatch.Groups[1].Value.Replace("\\\\", "\\") : "";
                        var exeMatch = Regex.Match(content, "\"LaunchExecutable\"\\s*:\\s*\"([^\"]+)\"");
                        string launchExe = exeMatch.Success ? exeMatch.Groups[1].Value : "";

                        if (sizeBytes <= 0 && !string.IsNullOrEmpty(installLoc) && Directory.Exists(installLoc))
                        {
                            sizeBytes = CalculateDirectorySize(installLoc);
                        }

                        DateTime? lastRun = DetermineLastRun(gameName, installLoc, launchExe, "Epic Games", userAssistRuns);

                        if (sizeBytes > 0)
                        {
                            if (apps.TryGetValue(gameName, out var existing))
                            {
                                existing.SizeBytes = Math.Max(existing.SizeBytes, sizeBytes);
                                if (lastRun.HasValue && (!existing.LastRunTime.HasValue || lastRun > existing.LastRunTime))
                                {
                                    existing.LastRunTime = lastRun;
                                }
                            }
                            else
                            {
                                apps[gameName] = new InstalledAppModel
                                {
                                    Name = gameName,
                                    Publisher = "Epic Games",
                                    Version = "Epic App",
                                    SizeBytes = sizeBytes,
                                    InstallLocation = installLoc,
                                    LastRunTime = lastRun
                                };
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        private static void ScanEaAndOriginGames(Dictionary<string, InstalledAppModel> apps, Dictionary<string, DateTime> userAssistRuns)
        {
            try
            {
                // Electronic Arts games (Battlefield, FIFA, Need for Speed, etc.)
                string[] regPaths = new[]
                {
                    @"SOFTWARE\Electronic Arts\EA Games",
                    @"SOFTWARE\Origin Games",
                    @"SOFTWARE\WOW6432Node\Electronic Arts\EA Games",
                    @"SOFTWARE\WOW6432Node\Origin Games"
                };

                foreach (var p in regPaths)
                {
                    using var key = Registry.LocalMachine.OpenSubKey(p);
                    if (key == null) continue;

                    foreach (var subName in key.GetSubKeyNames())
                    {
                        try
                        {
                            using var sub = key.OpenSubKey(subName);
                            if (sub == null) continue;

                            string? dir = sub.GetValue("Install Dir") as string ?? sub.GetValue("InstallDir") as string;
                            string? name = sub.GetValue("DisplayName") as string ?? subName;

                            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                            {
                                long size = CalculateDirectorySize(dir);
                                DateTime? lastRun = DetermineLastRun(name, dir, "", "Electronic Arts", userAssistRuns);

                                if (size > 0)
                                {
                                    if (apps.TryGetValue(name, out var existing))
                                    {
                                        existing.SizeBytes = Math.Max(existing.SizeBytes, size);
                                        if (lastRun.HasValue) existing.LastRunTime = lastRun;
                                    }
                                    else
                                    {
                                        apps[name] = new InstalledAppModel
                                        {
                                            Name = name,
                                            Publisher = "Electronic Arts",
                                            Version = "EA Game",
                                            SizeBytes = size,
                                            InstallLocation = dir,
                                            LastRunTime = lastRun
                                        };
                                    }
                                }
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static readonly HashSet<string> ExcludedExecutables = new(StringComparer.OrdinalIgnoreCase)
        {
            "unins000.exe", "unins001.exe", "uninstall.exe", "installer.exe", "setup.exe",
            "update.exe", "updater.exe", "autoupdate.exe", "helper.exe", "service.exe",
            "crashpad_handler.exe", "crashreporter.exe", "unitycrashhandler64.exe",
            "unitycrashhandler32.exe", "cmd.exe", "powershell.exe", "conhost.exe",
            "rundll32.exe", "regsvr32.exe", "dotnet.exe", "vcredist_x64.exe", "vcredist_x86.exe",
            "dxsetup.exe"
        };

        private static Dictionary<string, DateTime> LoadUserAssistHistory()
        {
            var dict = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using var root = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist");
                if (root == null) return dict;

                foreach (var guidKeyName in root.GetSubKeyNames())
                {
                    using var countKey = root.OpenSubKey($@"{guidKeyName}\Count");
                    if (countKey == null) continue;

                    foreach (var valueName in countKey.GetValueNames())
                    {
                        try
                        {
                            string decoded = Rot13(valueName);
                            if (!decoded.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                                !decoded.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            if (countKey.GetValue(valueName) is byte[] bytes && bytes.Length >= 68)
                            {
                                // In Windows 7/8/10/11: The last run FILETIME is at offset 60 (8 bytes)
                                long fileTime = BitConverter.ToInt64(bytes, 60);
                                if (fileTime > 0)
                                {
                                    var dt = DateTime.FromFileTimeUtc(fileTime).ToLocalTime();
                                    string fileName = Path.GetFileName(decoded);

                                    // Filter out common installers, updaters and crash handlers
                                    if (ExcludedExecutables.Contains(fileName))
                                        continue;

                                    if (!dict.TryGetValue(fileName, out var prev) || dt > prev)
                                    {
                                        dict[fileName] = dt;
                                    }

                                    // Index by full path
                                    dict[decoded] = dt;

                                    // Also index by filename without extension for shortcut resolution
                                    string nameWithoutExt = Path.GetFileNameWithoutExtension(decoded);
                                    if (!string.IsNullOrWhiteSpace(nameWithoutExt))
                                    {
                                        if (!dict.TryGetValue(nameWithoutExt, out var prev2) || dt > prev2)
                                        {
                                            dict[nameWithoutExt] = dt;
                                        }
                                    }
                                }
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {
            }

            return dict;
        }

        private static DateTime? DetermineLastRun(
            string appName,
            string installLocation,
            string displayIcon,
            string publisher,
            Dictionary<string, DateTime> userAssistRuns)
        {
            DateTime? maxTime = null;

            void Consider(DateTime? dt)
            {
                if (dt.HasValue && dt.Value > DateTime.MinValue && dt.Value <= DateTime.Now.AddMinutes(5))
                {
                    if (!maxTime.HasValue || dt.Value > maxTime.Value)
                    {
                        maxTime = dt.Value;
                    }
                }
            }

            try
            {
                // 1. Direct match via DisplayIcon (pointing to main app executable)
                if (!string.IsNullOrEmpty(displayIcon))
                {
                    string iconPath = CleanPath(displayIcon);
                    if (iconPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        string iconExe = Path.GetFileName(iconPath);
                        if (!ExcludedExecutables.Contains(iconExe))
                        {
                            if (userAssistRuns.TryGetValue(iconPath, out var uDtFull))
                                Consider(uDtFull);
                            else if (userAssistRuns.TryGetValue(iconExe, out var uDt))
                                Consider(uDt);

                            Consider(FindPrefetchTime(iconExe));
                        }
                    }
                }

                // 2. Direct match via application name shortcuts (.lnk) in UserAssist
                if (userAssistRuns.TryGetValue(appName + ".lnk", out var uDtLnk))
                {
                    Consider(uDtLnk);
                }

                string cleanAppName = Regex.Replace(appName, @"\s*\([^)]*\)", "").Trim();
                cleanAppName = Regex.Replace(cleanAppName, @"\b(Launcher|Client|Desktop|Edition|64-bit|32-bit)\b", "", RegexOptions.IgnoreCase).Trim();

                if (!string.IsNullOrEmpty(cleanAppName))
                {
                    if (userAssistRuns.TryGetValue(cleanAppName + ".lnk", out var uDtCleanLnk))
                        Consider(uDtCleanLnk);
                    if (userAssistRuns.TryGetValue(cleanAppName + ".exe", out var uDtCleanExe))
                        Consider(uDtCleanExe);
                    if (userAssistRuns.TryGetValue(cleanAppName, out var uDtCleanName))
                        Consider(uDtCleanName);
                }

                // 3. Inspect only top-level executables in installLocation
                if (!string.IsNullOrEmpty(installLocation) && Directory.Exists(installLocation))
                {
                    try
                    {
                        var topExes = Directory.EnumerateFiles(installLocation, "*.exe", SearchOption.TopDirectoryOnly);
                        foreach (var exe in topExes)
                        {
                            string exeName = Path.GetFileName(exe);
                            if (ExcludedExecutables.Contains(exeName)) continue;

                            if (userAssistRuns.TryGetValue(exe, out var dtFull))
                            {
                                Consider(dtFull);
                            }
                            else if (!string.IsNullOrEmpty(cleanAppName) &&
                                     (exeName.Contains(cleanAppName, StringComparison.OrdinalIgnoreCase) ||
                                      cleanAppName.Contains(Path.GetFileNameWithoutExtension(exeName), StringComparison.OrdinalIgnoreCase)))
                            {
                                if (userAssistRuns.TryGetValue(exeName, out var dtName))
                                    Consider(dtName);

                                Consider(FindPrefetchTime(exeName));
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            return maxTime;
        }

        private static DateTime? FindPrefetchTime(string exeName)
        {
            if (string.IsNullOrWhiteSpace(exeName)) return null;
            try
            {
                string prefetchDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");
                if (!Directory.Exists(prefetchDir)) return null;

                string baseName = Path.GetFileNameWithoutExtension(exeName).ToUpperInvariant();
                var files = Directory.EnumerateFiles(prefetchDir, $"{baseName}-*.pf");
                DateTime? latest = null;
                foreach (var f in files)
                {
                    try
                    {
                        var wt = File.GetLastWriteTime(f);
                        if (wt > DateTime.MinValue && wt <= DateTime.Now.AddMinutes(5))
                        {
                            if (!latest.HasValue || wt > latest.Value) latest = wt;
                        }
                    }
                    catch
                    {
                    }
                }
                return latest;
            }
            catch
            {
                return null;
            }
        }

        private static string ExtractFolderFromPath(string pathWithParams)
        {
            if (string.IsNullOrWhiteSpace(pathWithParams)) return "";
            string cleaned = CleanPath(pathWithParams);
            try
            {
                if (File.Exists(cleaned))
                {
                    return Path.GetDirectoryName(cleaned) ?? "";
                }
                if (Directory.Exists(cleaned))
                {
                    return cleaned;
                }
            }
            catch
            {
            }
            return "";
        }

        private static string CleanPath(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            string p = raw.Trim().Trim('"', '\'');
            int commaIdx = p.IndexOf(',');
            if (commaIdx > 0)
            {
                p = p.Substring(0, commaIdx).Trim().Trim('"', '\'');
            }
            return p;
        }

        private static long CalculateDirectorySize(string folderPath)
        {
            long size = 0;
            try
            {
                var dir = new DirectoryInfo(folderPath);
                foreach (var file in dir.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    try
                    {
                        size += file.Length;
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
            return size;
        }

        private static string Rot13(string input)
        {
            char[] array = input.ToCharArray();
            for (int i = 0; i < array.Length; i++)
            {
                int number = array[i];
                if (number >= 'a' && number <= 'z')
                {
                    if (number > 'm') number -= 13;
                    else number += 13;
                }
                else if (number >= 'A' && number <= 'Z')
                {
                    if (number > 'M') number -= 13;
                    else number += 13;
                }
                array[i] = (char)number;
            }
            return new string(array);
        }
    }
}
