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
    private readonly List<string> _scanPaths = new()
    {
        @"C:\Program Files",
        @"C:\Program Files (x86)",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
    };

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

        // Entertainment
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
    /// Scans the system for installed applications
    /// </summary>
    /// <param name="maxDepth">Maximum directory depth to scan (default: 2)</param>
    /// <returns>List of discovered applications</returns>
    public async Task<List<ApplicationInfo>> ScanApplicationsAsync(int maxDepth = 2)
    {
        _logger.LogInformation("Starting application scan with max depth: {MaxDepth}", maxDepth);

        var applications = new List<ApplicationInfo>();

        // Add system applications first
        applications.AddRange(_systemApps.Values);

        // Scan each path
        foreach (var basePath in _scanPaths)
        {
            if (!Directory.Exists(basePath))
            {
                _logger.LogWarning("Scan path does not exist: {Path}", basePath);
                continue;
            }

            _logger.LogInformation("Scanning path: {Path}", basePath);

            try
            {
                var foundApps = await Task.Run(() => ScanDirectory(basePath, 0, maxDepth));
                applications.AddRange(foundApps);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning path: {Path}", basePath);
            }
        }

        _logger.LogInformation("Application scan completed. Found {Count} applications", applications.Count);

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

        // Skip crash handlers and helpers
        if (fileName.Contains("crash") ||
            fileName.Contains("helper") ||
            fileName.Contains("service"))
        {
            return false;
        }

        return true;
    }
}
