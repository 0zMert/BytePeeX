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
                                    string exeName = Path.GetFileName(decoded);
                                    if (!dict.TryGetValue(exeName, out var prev) || dt > prev)
                                    {
                                        dict[exeName] = dt;
                                    }

                                    // Also index by full path
                                    dict[decoded] = dt;
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
                // 1. Check direct match in UserAssist for icon exe
                if (!string.IsNullOrEmpty(displayIcon))
                {
                    string iconPath = CleanPath(displayIcon);
                    string exeName = Path.GetFileName(iconPath);
                    if (!string.IsNullOrEmpty(exeName) && userAssistRuns.TryGetValue(exeName, out var dt))
                    {
                        Consider(dt);
                    }
                }

                // 2. Special detection for Epic Games & Epic Games Launcher
                if (appName.IndexOf("Epic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    installLocation.IndexOf("Epic", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string epicProgData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher");
                    if (Directory.Exists(epicProgData))
                    {
                        Consider(GetLatestFileTimeInFolders(epicProgData, new[] { "Data\\Catalog", "Data\\EMS", "Data", "Logs", "Saved", "" }));
                    }

                    string epicLocal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EpicGamesLauncher");
                    if (Directory.Exists(epicLocal))
                    {
                        Consider(GetLatestFileTimeInFolders(epicLocal, new[] { "Saved\\Logs", "Saved\\Config", "Saved", "" }));
                    }
                }

                // 3. Normalized app name for searching data folders and shortcuts
                string cleanAppName = Regex.Replace(appName, @"\s*\([^)]*\)", "").Trim();
                cleanAppName = Regex.Replace(cleanAppName, @"\b(Launcher|Client|Desktop|Edition|64-bit|32-bit)\b", "", RegexOptions.IgnoreCase).Trim();

                if (!string.IsNullOrEmpty(cleanAppName) && userAssistRuns.TryGetValue(cleanAppName + ".exe", out var uDtClean))
                {
                    Consider(uDtClean);
                }

                // 4. Check executables in installation folder (recursive up to 3 levels, max 25 files)
                if (!string.IsNullOrEmpty(installLocation) && Directory.Exists(installLocation))
                {
                    var exes = SafeEnumerateFiles(installLocation, "*.exe", maxFiles: 25, maxDepth: 3);
                    foreach (var exe in exes)
                    {
                        string name = Path.GetFileName(exe);
                        if (userAssistRuns.TryGetValue(name, out var uDt))
                        {
                            Consider(uDt);
                        }
                        else
                        {
                            try
                            {
                                var access = File.GetLastAccessTime(exe);
                                Consider(access);
                            }
                            catch
                            {
                            }
                        }
                    }
                }

                // 5. Check AppData and ProgramData runtime data folders
                var appDataRoots = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
                };

                var folderNamesToTry = new List<string>();
                if (!string.IsNullOrWhiteSpace(appName)) folderNamesToTry.Add(appName);
                if (!string.IsNullOrWhiteSpace(cleanAppName) && !folderNamesToTry.Contains(cleanAppName, StringComparer.OrdinalIgnoreCase))
                    folderNamesToTry.Add(cleanAppName);

                if (!string.IsNullOrEmpty(publisher))
                {
                    string cleanPub = Regex.Replace(publisher, @"\b(Inc\.|LLC|Corporation|Corp|GmbH|Software)\b", "", RegexOptions.IgnoreCase).Trim();
                    if (!string.IsNullOrWhiteSpace(cleanPub))
                    {
                        if (!string.IsNullOrWhiteSpace(cleanAppName)) folderNamesToTry.Add(Path.Combine(cleanPub, cleanAppName));
                        if (!string.IsNullOrWhiteSpace(appName)) folderNamesToTry.Add(Path.Combine(cleanPub, appName));
                    }
                }

                foreach (var root in appDataRoots)
                {
                    if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                    foreach (var sub in folderNamesToTry)
                    {
                        if (string.IsNullOrWhiteSpace(sub)) continue;
                        string fullPath = Path.Combine(root, sub);
                        if (Directory.Exists(fullPath))
                        {
                            Consider(GetLatestFileTimeInFolders(fullPath, new[] { "", "Logs", "Data", "Saved", "Cache", "Config" }));
                        }
                    }
                }

                // 6. Check Recent shortcuts
                if (!string.IsNullOrEmpty(cleanAppName) && cleanAppName.Length >= 3)
                {
                    try
                    {
                        string recent = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
                        if (Directory.Exists(recent))
                        {
                            var lnks = SafeEnumerateFiles(recent, $"*{cleanAppName}*.lnk", maxFiles: 5, maxDepth: 1);
                            foreach (var lnk in lnks)
                            {
                                try
                                {
                                    Consider(File.GetLastWriteTime(lnk));
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
            }
            catch
            {
            }

            return maxTime;
        }

        private static DateTime? GetLatestFileTimeInFolders(string rootDir, string[] subFolders)
        {
            DateTime? latest = null;
            foreach (var sub in subFolders)
            {
                string target = string.IsNullOrEmpty(sub) ? rootDir : Path.Combine(rootDir, sub);
                if (!Directory.Exists(target)) continue;

                try
                {
                    var files = SafeEnumerateFiles(target, "*.*", maxFiles: 15, maxDepth: 2);
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
                }
                catch
                {
                }
            }
            return latest;
        }

        private static List<string> SafeEnumerateFiles(string root, string pattern, int maxFiles, int maxDepth)
        {
            var results = new List<string>();
            void Walk(string current, int depth)
            {
                if (results.Count >= maxFiles || depth > maxDepth) return;
                try
                {
                    foreach (var file in Directory.EnumerateFiles(current, pattern))
                    {
                        results.Add(file);
                        if (results.Count >= maxFiles) return;
                    }
                    if (depth < maxDepth)
                    {
                        foreach (var dir in Directory.EnumerateDirectories(current))
                        {
                            Walk(dir, depth + 1);
                            if (results.Count >= maxFiles) return;
                        }
                    }
                }
                catch
                {
                }
            }
            Walk(root, 1);
            return results;
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
