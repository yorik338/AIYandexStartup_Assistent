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
    private readonly ApplicationRegistry _appRegistry;

    public WindowsActionExecutor(ILogger<WindowsActionExecutor> logger, ApplicationRegistry appRegistry)
    {
        _logger = logger;
        _pathValidator = new PathValidator();
        _appRegistry = appRegistry;
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
                "create_folder" => await CreateFolder(request),
                "delete_folder" => await DeleteFolder(request),
                "move_file" => await MoveFile(request),
                "copy_file" => await CopyFile(request),
                "scan_applications" => await ScanApplications(request),
                "list_applications" => await ListApplications(request),
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

            // Find application in registry
            var appInfo = _appRegistry.FindApplication(appName);
            if (appInfo == null)
            {
                return new CommandResponse
                {
                    Status = "error",
                    Result = null,
                    Error = $"Application '{appName}' not found in registry. Try running 'scan_applications' command first."
                };
            }

            _logger.LogInformation("Found application: {Name} at {Path}", appInfo.Name, appInfo.Path);

            // Build process start info
            var startInfo = new ProcessStartInfo
            {
                FileName = appInfo.Path,
                UseShellExecute = true
            };

            // Add launch arguments if specified
            if (!string.IsNullOrWhiteSpace(appInfo.LaunchArguments))
            {
                startInfo.Arguments = appInfo.LaunchArguments;
            }

            var process = Process.Start(startInfo);

            if (process == null)
            {
                return new CommandResponse
                {
                    Status = "error",
                    Result = null,
                    Error = $"Failed to start application: {appInfo.Name}"
                };
            }

            await Task.Delay(500); // Give process time to start

            return new CommandResponse
            {
                Status = "ok",
                Result = new
                {
                    application = appInfo.Name,
                    path = appInfo.Path,
                    category = appInfo.Category,
                    processId = process.Id,
                    message = $"Successfully opened {appInfo.Name}"
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

    private async Task<CommandResponse> CreateFolder(CommandRequest request)
    {
        var path = request.Params["path"].ToString();
        if (string.IsNullOrWhiteSpace(path))
        {
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = "Path is empty"
            };
        }

        try
        {
            _logger.LogInformation("Creating folder: {Path}", path);

            // Expand environment variables and make absolute path
            var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));

            // Validate path for security
            if (!_pathValidator.IsSafePath(fullPath))
            {
                return new CommandResponse
                {
                    Status = "error",
                    Result = null,
                    Error = "Path validation failed: Access to this path is forbidden"
                };
            }

            // Check if folder already exists
            if (Directory.Exists(fullPath))
            {
                return new CommandResponse
                {
                    Status = "ok",
                    Result = new
                    {
                        path = fullPath,
                        message = "Folder already exists",
                        alreadyExisted = true
                    },
                    Error = null
                };
            }

            // Create the folder
            await Task.Run(() => Directory.CreateDirectory(fullPath));

            return new CommandResponse
            {
                Status = "ok",
                Result = new
                {
                    path = fullPath,
                    message = "Folder created successfully",
                    alreadyExisted = false
                },
                Error = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create folder: {Path}", path);
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = $"Failed to create folder: {ex.Message}"
            };
        }
    }

    private async Task<CommandResponse> DeleteFolder(CommandRequest request)
    {
        var path = request.Params["path"].ToString();
        if (string.IsNullOrWhiteSpace(path))
        {
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = "Path is empty"
            };
        }

        try
        {
            _logger.LogInformation("Deleting folder: {Path}", path);

            // Expand environment variables and make absolute path
            var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));

            // Validate path for security
            if (!_pathValidator.IsSafePath(fullPath))
            {
                return new CommandResponse
                {
                    Status = "error",
                    Result = null,
                    Error = "Path validation failed: Access to this path is forbidden"
                };
            }

            // Check if folder exists
            if (!Directory.Exists(fullPath))
            {
                return new CommandResponse
                {
                    Status = "error",
                    Result = null,
                    Error = "Folder does not exist"
                };
            }

            // Delete the folder recursively
            await Task.Run(() => Directory.Delete(fullPath, recursive: true));

            return new CommandResponse
            {
                Status = "ok",
                Result = new
                {
                    path = fullPath,
                    message = "Folder deleted successfully"
                },
                Error = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete folder: {Path}", path);
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = $"Failed to delete folder: {ex.Message}"
            };
        }
    }

    private async Task<CommandResponse> MoveFile(CommandRequest request)
    {
        var source = request.Params["source"].ToString();
        var destination = request.Params["destination"].ToString();

        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(destination))
        {
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = "Source or destination path is empty"
            };
        }

        try
        {
            _logger.LogInformation("Moving file from {Source} to {Destination}", source, destination);

            // Expand environment variables and make absolute paths
            var sourcePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(source));
            var destPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(destination));

            // Validate both paths for security
            if (!_pathValidator.IsSafePath(sourcePath))
            {
                return new CommandResponse
                {
                    Status = "error",
                    Result = null,
                    Error = "Source path validation failed: Access to this path is forbidden"
                };
            }

            if (!_pathValidator.IsSafePath(destPath))
            {
                return new CommandResponse
                {
                    Status = "error",
                    Result = null,
                    Error = "Destination path validation failed: Access to this path is forbidden"
                };
            }

            // Check if source file exists
            if (!File.Exists(sourcePath))
            {
                return new CommandResponse
                {
                    Status = "error",
                    Result = null,
                    Error = "Source file does not exist"
                };
            }

            // Check if destination already exists
            if (File.Exists(destPath))
            {
                return new CommandResponse
                {
                    Status = "error",
                    Result = null,
                    Error = "Destination file already exists"
                };
            }

            // Move the file
            await Task.Run(() => File.Move(sourcePath, destPath));

            return new CommandResponse
            {
                Status = "ok",
                Result = new
                {
                    source = sourcePath,
                    destination = destPath,
                    message = "File moved successfully"
                },
                Error = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move file from {Source} to {Destination}", source, destination);
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = $"Failed to move file: {ex.Message}"
            };
        }
    }

    private async Task<CommandResponse> CopyFile(CommandRequest request)
    {
        var source = request.Params["source"].ToString();
        var destination = request.Params["destination"].ToString();

        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(destination))
        {
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = "Source or destination path is empty"
            };
        }

        try
        {
            _logger.LogInformation("Copying file from {Source} to {Destination}", source, destination);

            // Expand environment variables and make absolute paths
            var sourcePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(source));
            var destPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(destination));

            // Validate both paths for security
            if (!_pathValidator.IsSafePath(sourcePath))
            {
                return new CommandResponse
                {
                    Status = "error",
                    Result = null,
                    Error = "Source path validation failed: Access to this path is forbidden"
                };
            }

            if (!_pathValidator.IsSafePath(destPath))
            {
                return new CommandResponse
                {
                    Status = "error",
                    Result = null,
                    Error = "Destination path validation failed: Access to this path is forbidden"
                };
            }

            // Check if source file exists
            if (!File.Exists(sourcePath))
            {
                return new CommandResponse
                {
                    Status = "error",
                    Result = null,
                    Error = "Source file does not exist"
                };
            }

            // Check if destination already exists
            if (File.Exists(destPath))
            {
                return new CommandResponse
                {
                    Status = "error",
                    Result = null,
                    Error = "Destination file already exists"
                };
            }

            // Copy the file
            await Task.Run(() => File.Copy(sourcePath, destPath));

            return new CommandResponse
            {
                Status = "ok",
                Result = new
                {
                    source = sourcePath,
                    destination = destPath,
                    message = "File copied successfully"
                },
                Error = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy file from {Source} to {Destination}", source, destination);
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = $"Failed to copy file: {ex.Message}"
            };
        }
    }

    private async Task<CommandResponse> ScanApplications(CommandRequest request)
    {
        try
        {
            _logger.LogInformation("Starting application scan...");

            await _appRegistry.ScanAndSaveAsync();

            var stats = _appRegistry.GetStatistics();

            return new CommandResponse
            {
                Status = "ok",
                Result = new
                {
                    message = "Application scan completed successfully",
                    statistics = stats
                },
                Error = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan applications");
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = $"Failed to scan applications: {ex.Message}"
            };
        }
    }

    private async Task<CommandResponse> ListApplications(CommandRequest request)
    {
        try
        {
            await Task.CompletedTask;

            // Check if category filter is specified
            string? category = null;
            if (request.Params.ContainsKey("category"))
            {
                category = request.Params["category"]?.ToString();
            }

            var apps = string.IsNullOrWhiteSpace(category)
                ? _appRegistry.GetAllApplications()
                : _appRegistry.GetApplicationsByCategory(category);

            var stats = _appRegistry.GetStatistics();

            return new CommandResponse
            {
                Status = "ok",
                Result = new
                {
                    applications = apps.Select(a => new
                    {
                        name = a.Name,
                        category = a.Category,
                        path = a.Path,
                        aliases = a.Aliases,
                        isSystemApp = a.IsSystemApp
                    }).ToList(),
                    count = apps.Count,
                    statistics = stats
                },
                Error = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list applications");
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = $"Failed to list applications: {ex.Message}"
            };
        }
    }
}
