using JarvisCore.Models;
using System.Text.Json;

namespace JarvisCore.Services;

/// <summary>
/// Manages the registry of discovered applications
/// Handles loading, saving, and searching the application database
/// </summary>
public class ApplicationRegistry
{
    private readonly ILogger<ApplicationRegistry> _logger;
    private readonly ApplicationScanner _scanner;
    private readonly string _registryPath;
    private List<ApplicationInfo> _applications = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ApplicationRegistry(ILogger<ApplicationRegistry> logger, ApplicationScanner scanner)
    {
        _logger = logger;
        _scanner = scanner;

        // Registry file location: /core/Data/applications.json
        var dataFolder = Path.Combine(AppContext.BaseDirectory, "Data");
        Directory.CreateDirectory(dataFolder);
        _registryPath = Path.Combine(dataFolder, "applications.json");

        _logger.LogInformation("Application registry path: {Path}", _registryPath);
    }

    /// <summary>
    /// Initializes the registry by loading from file (does NOT auto-scan)
    /// Use ScanAndSaveAsync() to manually scan the system
    /// </summary>
    public async Task InitializeAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (File.Exists(_registryPath))
            {
                _logger.LogInformation("Loading application registry from: {Path}", _registryPath);
                await LoadFromFileAsync();
            }
            else
            {
                _logger.LogWarning("No registry found at {Path}. Use 'scan_applications' command to populate.", _registryPath);
                // Start with only system apps
                _applications = new List<ApplicationInfo>(_scanner.GetSystemApplications());
            }

            _logger.LogInformation("Application registry initialized with {Count} applications", _applications.Count);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Scans the system for applications and updates the registry
    /// </summary>
    public async Task ScanAndSaveAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _logger.LogInformation("Scanning system for applications...");
            _applications = await _scanner.ScanApplicationsAsync();

            _logger.LogInformation("Saving {Count} applications to registry", _applications.Count);
            await SaveToFileAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Finds an application by name or alias
    /// </summary>
    /// <param name="nameOrAlias">Application name or alias to search for</param>
    /// <returns>ApplicationInfo if found, null otherwise</returns>
    public ApplicationInfo? FindApplication(string nameOrAlias)
    {
        if (string.IsNullOrWhiteSpace(nameOrAlias))
            return null;

        var searchTerm = nameOrAlias.ToLowerInvariant().Trim();

        // First try exact name match
        var app = _applications.FirstOrDefault(a =>
            a.Name.Equals(searchTerm, StringComparison.OrdinalIgnoreCase));

        if (app != null)
            return app;

        // Then try alias match
        app = _applications.FirstOrDefault(a =>
            a.Aliases.Any(alias => alias.Equals(searchTerm, StringComparison.OrdinalIgnoreCase)));

        if (app != null)
            return app;

        // Finally try partial match
        app = _applications.FirstOrDefault(a =>
            a.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));

        return app;
    }

    /// <summary>
    /// Gets all registered applications
    /// </summary>
    public List<ApplicationInfo> GetAllApplications()
    {
        return new List<ApplicationInfo>(_applications);
    }

    /// <summary>
    /// Gets applications by category
    /// </summary>
    public List<ApplicationInfo> GetApplicationsByCategory(string category)
    {
        return _applications
            .Where(a => a.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Gets registry statistics
    /// </summary>
    public object GetStatistics()
    {
        var categories = _applications
            .GroupBy(a => a.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        return new
        {
            TotalApplications = _applications.Count,
            SystemApplications = _applications.Count(a => a.IsSystemApp),
            UserApplications = _applications.Count(a => !a.IsSystemApp),
            Categories = categories,
            LastUpdated = File.Exists(_registryPath) ? File.GetLastWriteTimeUtc(_registryPath) : (DateTime?)null
        };
    }

    private async Task LoadFromFileAsync()
    {
        try
        {
            var json = await File.ReadAllTextAsync(_registryPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };

            _applications = JsonSerializer.Deserialize<List<ApplicationInfo>>(json, options) ?? new List<ApplicationInfo>();
            _logger.LogInformation("Loaded {Count} applications from registry", _applications.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load application registry from file");
            _applications = new List<ApplicationInfo>();
        }
    }

    private async Task SaveToFileAsync()
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(_applications, options);
            await File.WriteAllTextAsync(_registryPath, json);

            _logger.LogInformation("Application registry saved successfully to {Path}", _registryPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save application registry to file");
            throw;
        }
    }
}
