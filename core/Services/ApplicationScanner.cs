using JarvisCore.Models;
using System.Diagnostics;

namespace JarvisCore.Services;

/// <summary>
/// Scans the system for installed applications
/// </summary>
public class ApplicationScanner
{
    private readonly ILogger<ApplicationScanner> _logger;

    // Common folders where applications are installed
    // AUTOMATED: Dynamically finds Steam libraries, all drives, and game folders
    private List<string> GetScanPaths()
    {
        var paths = new List<string>
        {
            // Program Files (most applications)
            @"C:\Program Files",
            @"C:\Program Files (x86)",

            // LocalAppData - specific known folders only
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Discord"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "slack"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Obsidian"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JetBrains"),

            // AppData - specific known folders only
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Telegram Desktop"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Spotify"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Zoom"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Notion")
        };

        // AUTOMATED: Find all Steam library folders by reading libraryfolders.vdf
        var steamLibraries = GetSteamLibraryPaths();
        foreach (var library in steamLibraries)
        {
            paths.Add(Path.Combine(library, "steamapps", "common"));
            _logger.LogInformation("Auto-detected Steam library: {Path}", library);
        }

        // AUTOMATED: Scan all available drives for game folders
        var gamePaths = GetGamePathsFromAllDrives();
        paths.AddRange(gamePaths);

        return paths;
    }

    /// <summary>
    /// Automatically finds all Steam library folders by reading libraryfolders.vdf
    /// </summary>
    private List<string> GetSteamLibraryPaths()
    {
        var libraries = new List<string>();

        // Possible Steam installation locations
        var steamPaths = new[]
        {
            @"C:\Program Files (x86)\Steam",
            @"C:\Program Files\Steam",
            @"D:\Steam",
            @"E:\Steam"
        };

        foreach (var steamPath in steamPaths)
        {
            var configPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (File.Exists(configPath))
            {
                try
                {
                    var content = File.ReadAllText(configPath);

                    // Parse VDF file to extract library paths
                    // Format: "path"		"D:\\SteamLibrary"
                    var pathPattern = @"""path""\s+""([^""]+)""";
                    var matches = System.Text.RegularExpressions.Regex.Matches(content, pathPattern);

                    foreach (System.Text.RegularExpressions.Match match in matches)
                    {
                        var libraryPath = match.Groups[1].Value.Replace("\\\\", "\\");
                        if (Directory.Exists(libraryPath))
                        {
                            libraries.Add(libraryPath);
                            _logger.LogInformation("Found Steam library: {Path}", libraryPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read Steam library config at {Path}", configPath);
                }

                break; // Found Steam installation, no need to check other locations
            }
        }

        return libraries;
    }

    /// <summary>
    /// Scans all available drives for common game installation folders
    /// </summary>
    private List<string> GetGamePathsFromAllDrives()
    {
        var gamePaths = new List<string>();

        // Get all logical drives (C:\, D:\, E:\, etc.)
        var drives = DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
            .Select(d => d.Name)
            .ToList();

        _logger.LogInformation("Scanning {Count} drives for game folders: {Drives}",
            drives.Count, string.Join(", ", drives));

        foreach (var drive in drives)
        {
            // Common game folder patterns on each drive
            var commonGamePaths = new[]
            {
                Path.Combine(drive, "Epic Games"),
                Path.Combine(drive, "Program Files", "Epic Games"),
                Path.Combine(drive, "GOG Games"),
                Path.Combine(drive, "Games"),
                Path.Combine(drive, "Program Files", "Riot Games"),
                Path.Combine(drive, "Program Files (x86)", "Riot Games")
            };

            foreach (var path in commonGamePaths)
            {
                if (Directory.Exists(path))
                {
                    gamePaths.Add(path);
                    _logger.LogInformation("Auto-detected game folder: {Path}", path);
                }
            }
        }

        return gamePaths;
    }

    // Known popular applications with exact paths (high priority)
    // These override scanner results to ensure correct .exe files
    private Dictionary<string, ApplicationInfo> GetKnownApplications()
    {
        var known = new Dictionary<string, ApplicationInfo>(StringComparer.OrdinalIgnoreCase);

        // Steam - exact path to main executable
        var steamPath = @"C:\Program Files (x86)\Steam\steam.exe";
        if (File.Exists(steamPath))
        {
            known["steam"] = new ApplicationInfo
            {
                Id = "known_steam",
                Name = "Steam",
                ExecutableName = "steam.exe",
                Path = steamPath,
                Category = "Entertainment",
                IsSystemApp = false,
                Aliases = new List<string> { "steam", "стим" }
            };
        }

        // Discord - exact path
        var discordPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Discord\app-1.0.9162\Discord.exe"
        );
        // Discord updates frequently, so find latest version
        var discordBase = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Discord"
        );
        if (Directory.Exists(discordBase))
        {
            var appDirs = Directory.GetDirectories(discordBase, "app-*")
                .OrderByDescending(d => d)
                .ToList();

            if (appDirs.Any())
            {
                var latestDiscord = Path.Combine(appDirs[0], "Discord.exe");
                if (File.Exists(latestDiscord))
                {
                    known["discord"] = new ApplicationInfo
                    {
                        Id = "known_discord",
                        Name = "Discord",
                        ExecutableName = "Discord.exe",
                        Path = latestDiscord,
                        Category = "Communication",
                        IsSystemApp = false,
                        Aliases = new List<string> { "discord", "дискорд" }
                    };
                }
            }
        }

        // Telegram - exact path
        var telegramPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Telegram Desktop\Telegram.exe"
        );
        if (File.Exists(telegramPath))
        {
            known["telegram"] = new ApplicationInfo
            {
                Id = "known_telegram",
                Name = "Telegram",
                ExecutableName = "Telegram.exe",
                Path = telegramPath,
                Category = "Communication",
                IsSystemApp = false,
                Aliases = new List<string> { "telegram", "телеграм" }
            };
        }

        // Spotify - exact path
        var spotifyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Spotify\Spotify.exe"
        );
        if (File.Exists(spotifyPath))
        {
            known["spotify"] = new ApplicationInfo
            {
                Id = "known_spotify",
                Name = "Spotify",
                ExecutableName = "Spotify.exe",
                Path = spotifyPath,
                Category = "Entertainment",
                IsSystemApp = false,
                Aliases = new List<string> { "spotify", "спотифай" }
            };
        }

        // VS Code - exact path
        var vscodePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Programs\Microsoft VS Code\Code.exe"
        );
        if (File.Exists(vscodePath))
        {
            known["vscode"] = new ApplicationInfo
            {
                Id = "known_vscode",
                Name = "Visual Studio Code",
                ExecutableName = "Code.exe",
                Path = vscodePath,
                Category = "Development",
                IsSystemApp = false,
                Aliases = new List<string> { "vscode", "code", "visual studio code" }
            };
        }

        return known;
    }

    // System applications that don't require scanning
    private readonly Dictionary<string, ApplicationInfo> _systemApps = new()
    {
        {
            "notepad", new ApplicationInfo
            {
                Id = "system_notepad",
                Name = "Notepad",
                ExecutableName = "notepad.exe",
                Path = "notepad.exe",
                Category = "System",
                IsSystemApp = true,
                Aliases = new List<string> { "notepad", "блокнот" }
            }
        },
        {
            "calculator", new ApplicationInfo
            {
                Id = "system_calculator",
                Name = "Calculator",
                ExecutableName = "calc.exe",
                Path = "calc.exe",
                Category = "System",
                IsSystemApp = true,
                Aliases = new List<string> { "calculator", "calc", "калькулятор" }
            }
        },
        {
            "paint", new ApplicationInfo
            {
                Id = "system_paint",
                Name = "Paint",
                ExecutableName = "mspaint.exe",
                Path = "mspaint.exe",
                Category = "System",
                IsSystemApp = true,
                Aliases = new List<string> { "paint", "mspaint" }
            }
        },
        {
            "explorer", new ApplicationInfo
            {
                Id = "system_explorer",
                Name = "Explorer",
                ExecutableName = "explorer.exe",
                Path = "explorer.exe",
                Category = "System",
                IsSystemApp = true,
                Aliases = new List<string> { "explorer", "проводник" }
            }
        },
        {
            "cmd", new ApplicationInfo
            {
                Id = "system_cmd",
                Name = "Command Prompt",
                ExecutableName = "cmd.exe",
                Path = "cmd.exe",
                Category = "System",
                IsSystemApp = true,
                Aliases = new List<string> { "cmd", "command" }
            }
        },
        {
            "powershell", new ApplicationInfo
            {
                Id = "system_powershell",
                Name = "PowerShell",
                ExecutableName = "powershell.exe",
                Path = "powershell.exe",
                Category = "System",
                IsSystemApp = true,
                Aliases = new List<string> { "powershell", "ps" }
            }
        },
        {
            "terminal", new ApplicationInfo
            {
                Id = "system_terminal",
                Name = "Windows Terminal",
                ExecutableName = "wt.exe",
                Path = "wt.exe",
                Category = "System",
                IsSystemApp = true,
                Aliases = new List<string> { "terminal", "wt" }
            }
        }
    };

    // Known application patterns for better categorization
    private readonly Dictionary<string, string> _categoryPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        // Communication
        { "discord", "Communication" },
        { "telegram", "Communication" },
        { "slack", "Communication" },
        { "teams", "Communication" },
        { "skype", "Communication" },
        { "zoom", "Communication" },

        // Development
        { "code", "Development" },
        { "vscode", "Development" },
        { "visual studio", "Development" },
        { "rider", "Development" },
        { "pycharm", "Development" },
        { "intellij", "Development" },
        { "webstorm", "Development" },
        { "android studio", "Development" },
        { "git", "Development" },

        // Browsers
        { "chrome", "Browser" },
        { "firefox", "Browser" },
        { "edge", "Browser" },
        { "opera", "Browser" },
        { "brave", "Browser" },

        // Games (check for game-specific keywords)
        { "counter-strike", "Games" },
        { "cs2", "Games" },
        { "csgo", "Games" },
        { "dota", "Games" },
        { "valorant", "Games" },
        { "league of legends", "Games" },
        { "minecraft", "Games" },
        { "fortnite", "Games" },
        { "apex", "Games" },
        { "gta", "Games" },
        { "witcher", "Games" },
        { "cyberpunk", "Games" },
        { "elden ring", "Games" },
        { "terraria", "Games" },
        { "rust", "Games" },
        { "overwatch", "Games" },
        { "warzone", "Games" },
        { "battlefield", "Games" },
        { "call of duty", "Games" },
        { "fallout", "Games" },
        { "skyrim", "Games" },
        { "satisfactory", "Games" },
        { "factorio", "Games" },

        // Entertainment (launchers and media)
        { "spotify", "Entertainment" },
        { "steam", "Entertainment" },
        { "epic", "Entertainment" },
        { "vlc", "Entertainment" },

        // Utilities
        { "notion", "Utilities" },
        { "obsidian", "Utilities" },
        { "evernote", "Utilities" }
    };

    public ApplicationScanner(ILogger<ApplicationScanner> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the list of system applications (no scanning required)
    /// </summary>
    public virtual List<ApplicationInfo> GetSystemApplications()
    {
        return new List<ApplicationInfo>(_systemApps.Values);
    }

    /// <summary>
    /// Scans the system for installed applications
    /// OPTIMIZED: Parallel scanning with depth 2 to find games in subfolders
    /// </summary>
    /// <param name="maxDepth">Maximum directory depth to scan (default: 2, finds games and apps)</param>
    /// <returns>List of discovered applications</returns>
    public virtual async Task<List<ApplicationInfo>> ScanApplicationsAsync(int maxDepth = 2)
    {
        _logger.LogInformation("Starting OPTIMIZED application scan with max depth: {MaxDepth}", maxDepth);

        var scanPaths = GetScanPaths();
        _logger.LogInformation("Scanning {Count} paths in parallel...", scanPaths.Count);

        var applications = new List<ApplicationInfo>();
        var knownAppPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // PRIORITY 1: Add system applications first
        applications.AddRange(_systemApps.Values);
        _logger.LogInformation("Added {Count} system applications", _systemApps.Count);

        // PRIORITY 2: Add known applications with exact paths (Steam, Discord, etc.)
        var knownApps = GetKnownApplications();
        applications.AddRange(knownApps.Values);
        foreach (var app in knownApps.Values)
        {
            knownAppPaths.Add(app.Path);
        }
        _logger.LogInformation("Added {Count} known applications with exact paths", knownApps.Count);

        // PRIORITY 3: Scan all paths in parallel for other applications
        var scanTasks = scanPaths
            .Where(Directory.Exists)
            .Select(async basePath =>
            {
                _logger.LogInformation("Scanning path: {Path}", basePath);
                try
                {
                    return await Task.Run(() => ScanDirectory(basePath, 0, maxDepth));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error scanning path: {Path}", basePath);
                    return new List<ApplicationInfo>();
                }
            })
            .ToList();

        // Wait for all scans to complete in parallel
        var results = await Task.WhenAll(scanTasks);

        // Combine results, skipping duplicates from known apps
        int skippedDuplicates = 0;
        foreach (var foundApps in results)
        {
            foreach (var app in foundApps)
            {
                // Skip if this path is already in known apps (avoid duplicates)
                if (knownAppPaths.Contains(app.Path))
                {
                    skippedDuplicates++;
                    continue;
                }

                applications.Add(app);
            }
        }

        _logger.LogInformation("Application scan completed. Found {Count} applications ({Skipped} duplicates skipped)",
            applications.Count, skippedDuplicates);

        return applications;
    }

    private List<ApplicationInfo> ScanDirectory(string path, int currentDepth, int maxDepth)
    {
        var applications = new List<ApplicationInfo>();

        if (currentDepth > maxDepth)
            return applications;

        try
        {
            // Find all .exe files in current directory
            var exeFiles = Directory.GetFiles(path, "*.exe", SearchOption.TopDirectoryOnly);

            foreach (var exePath in exeFiles)
            {
                try
                {
                    var appInfo = CreateApplicationInfo(exePath);
                    if (appInfo != null && IsValidApplication(appInfo))
                    {
                        applications.Add(appInfo);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not process executable: {Path}", exePath);
                }
            }

            // Recursively scan subdirectories
            var subdirectories = Directory.GetDirectories(path);
            foreach (var subdir in subdirectories)
            {
                try
                {
                    // Skip common folders that don't contain main executables
                    var dirName = Path.GetFileName(subdir).ToLowerInvariant();
                    if (dirName == "cache" || dirName == "temp" || dirName == "logs" || dirName == "data")
                        continue;

                    var subApps = ScanDirectory(subdir, currentDepth + 1, maxDepth);
                    applications.AddRange(subApps);
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip directories we can't access
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not scan subdirectory: {Path}", subdir);
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip directories we can't access
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error scanning directory: {Path}", path);
        }

        return applications;
    }

    private ApplicationInfo? CreateApplicationInfo(string exePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(exePath);
        var executableName = Path.GetFileName(exePath);

        // Try to get file version info for better app name
        string displayName = fileName;
        try
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
            if (!string.IsNullOrWhiteSpace(versionInfo.ProductName))
            {
                displayName = versionInfo.ProductName;
            }
            else if (!string.IsNullOrWhiteSpace(versionInfo.FileDescription))
            {
                displayName = versionInfo.FileDescription;
            }
        }
        catch
        {
            // If we can't get version info, use file name
        }

        var category = DetermineCategory(displayName, fileName);
        var aliases = GenerateAliases(displayName, fileName);

        return new ApplicationInfo
        {
            Id = $"app_{Guid.NewGuid():N}",
            Name = displayName,
            Path = exePath,
            ExecutableName = executableName,
            Category = category,
            Aliases = aliases,
            IsSystemApp = false,
            DiscoveredAt = DateTime.UtcNow
        };
    }

    private string DetermineCategory(string displayName, string fileName)
    {
        var combinedName = $"{displayName} {fileName}".ToLowerInvariant();

        foreach (var pattern in _categoryPatterns)
        {
            if (combinedName.Contains(pattern.Key.ToLowerInvariant()))
            {
                return pattern.Value;
            }
        }

        return "Other";
    }

    private List<string> GenerateAliases(string displayName, string fileName)
    {
        var aliases = new List<string>
        {
            displayName.ToLowerInvariant(),
            fileName.ToLowerInvariant()
        };

        // Remove duplicates
        aliases = aliases.Distinct().ToList();

        // Add Russian translations for common apps
        var russianAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "discord", "дискорд" },
            { "telegram", "телеграм" },
            { "spotify", "спотифай" },
            { "steam", "стим" },
            { "chrome", "хром" },
            { "firefox", "фаерфокс" }
        };

        foreach (var alias in aliases.ToList())
        {
            if (russianAliases.ContainsKey(alias))
            {
                aliases.Add(russianAliases[alias]);
            }
        }

        return aliases;
    }

    private bool IsValidApplication(ApplicationInfo appInfo)
    {
        // Filter out unwanted executables
        var fileName = appInfo.ExecutableName.ToLowerInvariant();

        // Skip installers, updaters, uninstallers
        if (fileName.Contains("install") ||
            fileName.Contains("uninstall") ||
            fileName.Contains("update") ||
            fileName.Contains("setup") ||
            fileName.Contains("uninst"))
        {
            return false;
        }

        // Skip crash handlers, helpers, services, overlays
        if (fileName.Contains("crash") ||
            fileName.Contains("helper") ||
            fileName.Contains("service") ||
            fileName.Contains("overlay") ||
            fileName.Contains("launcher") ||
            fileName.Contains("reporter") ||
            fileName.Contains("error"))
        {
            return false;
        }

        // Skip common background processes
        if (fileName.Contains("background") ||
            fileName.Contains("daemon") ||
            fileName.Contains("watchdog") ||
            fileName.Contains("monitor"))
        {
            return false;
        }

        return true;
    }
}
