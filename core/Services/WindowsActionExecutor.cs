using JarvisCore.Models;
using JarvisCore.Security;
using System.Diagnostics;
using System.Text.Json;

namespace JarvisCore.Services;

/// <summary>
/// Windows Action Engine - executes system actions on Windows
/// </summary>
public class WindowsActionExecutor : IActionExecutor
{
    private readonly ILogger<WindowsActionExecutor> _logger;
    private readonly PathValidator _pathValidator;

    public WindowsActionExecutor(ILogger<WindowsActionExecutor> logger)
    {
        _logger = logger;
        _pathValidator = new PathValidator();
    }

    public async Task<CommandResponse> ExecuteAsync(CommandRequest request)
    {
        try
        {
            _logger.LogInformation("Executing action: {Action} with UUID: {Uuid}", request.Action, request.Uuid);

            return request.Action switch
            {
                "open_app" => await OpenApplication(request),
                "search_files" => await SearchFiles(request),
                "adjust_setting" => await AdjustSetting(request),
                "system_status" => await GetSystemStatus(request),
                _ => new CommandResponse
                {
                    Status = "error",
                    Result = null,
                    Error = $"Unknown action: {request.Action}"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing action {Action}", request.Action);
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = $"Execution failed: {ex.Message}"
            };
        }
    }

    private async Task<CommandResponse> OpenApplication(CommandRequest request)
    {
        var appName = request.Params["application"].ToString();
        if (string.IsNullOrWhiteSpace(appName))
        {
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = "Application name is empty"
            };
        }

        try
        {
            _logger.LogInformation("Opening application: {AppName}", appName);

            // Common Windows applications mapping
            var appMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "notepad", "notepad.exe" },
                { "блокнот", "notepad.exe" },
                { "calculator", "calc.exe" },
                { "калькулятор", "calc.exe" },
                { "explorer", "explorer.exe" },
                { "проводник", "explorer.exe" },
                { "paint", "mspaint.exe" },
                { "chrome", "chrome.exe" },
                { "firefox", "firefox.exe" },
                { "edge", "msedge.exe" }
            };

            var executableName = appMappings.ContainsKey(appName)
                ? appMappings[appName]
                : appName;

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = executableName,
                UseShellExecute = true
            });

            if (process == null)
            {
                return new CommandResponse
                {
                    Status = "error",
                    Result = null,
                    Error = $"Failed to start application: {appName}"
                };
            }

            await Task.Delay(500); // Give process time to start

            return new CommandResponse
            {
                Status = "ok",
                Result = new
                {
                    application = appName,
                    processId = process.Id,
                    message = $"Successfully opened {appName}"
                },
                Error = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open application: {AppName}", appName);
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = $"Failed to open {appName}: {ex.Message}"
            };
        }
    }

    private async Task<CommandResponse> SearchFiles(CommandRequest request)
    {
        var query = request.Params["query"].ToString();
        if (string.IsNullOrWhiteSpace(query))
        {
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = "Search query is empty"
            };
        }

        try
        {
            _logger.LogInformation("Searching files with query: {Query}", query);

            // Search in user's Documents and Desktop folders for safety
            var searchPaths = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            var results = new List<string>();

            foreach (var basePath in searchPaths)
            {
                if (!Directory.Exists(basePath))
                    continue;

                var files = await Task.Run(() =>
                    Directory.GetFiles(basePath, $"*{query}*", SearchOption.TopDirectoryOnly)
                        .Take(10) // Limit results
                        .ToList()
                );

                results.AddRange(files);
            }

            return new CommandResponse
            {
                Status = "ok",
                Result = new
                {
                    query,
                    filesFound = results.Count,
                    files = results.Select(Path.GetFileName).ToList()
                },
                Error = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search files with query: {Query}", query);
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = $"Search failed: {ex.Message}"
            };
        }
    }

    private async Task<CommandResponse> AdjustSetting(CommandRequest request)
    {
        var setting = request.Params["setting"].ToString();
        var value = request.Params["value"].ToString();

        _logger.LogInformation("Adjusting setting: {Setting} to {Value}", setting, value);

        // Placeholder for future implementation
        // This would integrate with Windows settings APIs
        await Task.CompletedTask;

        return new CommandResponse
        {
            Status = "ok",
            Result = new
            {
                setting,
                value,
                message = "Setting adjustment is not yet implemented. This is a placeholder."
            },
            Error = null
        };
    }

    private async Task<CommandResponse> GetSystemStatus(CommandRequest request)
    {
        _logger.LogInformation("Getting system status");

        await Task.CompletedTask;

        var cpuCount = Environment.ProcessorCount;
        var osVersion = Environment.OSVersion.VersionString;
        var machineName = Environment.MachineName;
        var userName = Environment.UserName;
        var uptime = Environment.TickCount64;

        return new CommandResponse
        {
            Status = "ok",
            Result = new
            {
                machineName,
                userName,
                osVersion,
                cpuCount,
                uptimeMs = uptime,
                timestamp = DateTime.UtcNow.ToString("O")
            },
            Error = null
        };
    }
}
