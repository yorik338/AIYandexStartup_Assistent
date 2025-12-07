namespace JarvisCore.Security;

/// <summary>
/// Validates file paths to prevent access to critical system directories
/// </summary>
public class PathValidator
{
    // List of forbidden paths (case-insensitive)
    private static readonly string[] ForbiddenPaths = new[]
    {
        @"C:\Windows",
        @"C:\Windows\System32",
        @"C:\Windows\SysWOW64",
        @"C:\Program Files",
        @"C:\Program Files (x86)",
        @"C:\Program Files\WindowsApps",
        @"C:\ProgramData\Microsoft\Windows\Start Menu",
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32")
    };

    // Root drives that should not be allowed
    private static readonly string[] ForbiddenRootDrives = new[]
    {
        @"C:\",
        @"D:\",
        @"E:\",
        @"F:\"
    };

    /// <summary>
    /// Check if path is safe to access
    /// </summary>
    public bool IsSafePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            var fullPath = Path.GetFullPath(path);

            // Check against forbidden paths
            foreach (var forbidden in ForbiddenPaths)
            {
                if (fullPath.StartsWith(forbidden, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
        catch
        {
            // If path is invalid, consider it unsafe
            return false;
        }
    }

    /// <summary>
    /// Static method to validate if a path is safe (for unit testing and convenience)
    /// </summary>
    public static bool IsValidPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            var fullPath = Path.GetFullPath(path);

            // Check if it's a root drive
            foreach (var root in ForbiddenRootDrives)
            {
                if (fullPath.Equals(root, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            // Check against forbidden paths
            foreach (var forbidden in ForbiddenPaths)
            {
                if (fullPath.StartsWith(forbidden, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
        catch
        {
            // If path is invalid, consider it unsafe
            return false;
        }
    }

    /// <summary>
    /// Expands environment variables in a path
    /// </summary>
    public static string ExpandPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        return Environment.ExpandEnvironmentVariables(path);
    }

    /// <summary>
    /// Get list of allowed base directories for file operations
    /// </summary>
    public static List<string> GetAllowedBasePaths()
    {
        return new List<string>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
        };
    }
}
