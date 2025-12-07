namespace JarvisCore.Models;

/// <summary>
/// Represents information about a discovered application
/// </summary>
public class ApplicationInfo
{
    /// <summary>
    /// Unique identifier for the application
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the application (e.g., "Discord", "Visual Studio Code")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Full path to the executable (e.g., "C:\Program Files\Discord\Discord.exe")
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Executable file name (e.g., "Discord.exe")
    /// </summary>
    public string ExecutableName { get; set; } = string.Empty;

    /// <summary>
    /// Category of the application (e.g., "Communication", "Development", "Entertainment")
    /// </summary>
    public string Category { get; set; } = "Other";

    /// <summary>
    /// Alternative names and aliases for the application
    /// Including Russian translations (e.g., ["discord", "дискорд"])
    /// </summary>
    public List<string> Aliases { get; set; } = new();

    /// <summary>
    /// When the application was discovered
    /// </summary>
    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional command line arguments needed to launch (optional)
    /// </summary>
    public string? LaunchArguments { get; set; }

    /// <summary>
    /// Whether this application is a system application
    /// </summary>
    public bool IsSystemApp { get; set; } = false;
}
