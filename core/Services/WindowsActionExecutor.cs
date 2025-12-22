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
    private readonly WindowCaptureService _windowCaptureService;
    private readonly MicrophoneRecorder _microphoneRecorder;

    public WindowsActionExecutor(ILogger<WindowsActionExecutor> logger, ApplicationRegistry appRegistry, WindowCaptureService windowCaptureService, MicrophoneRecorder microphoneRecorder)
    {
        _logger = logger;
        _pathValidator = new PathValidator();
        _appRegistry = appRegistry;
        _windowCaptureService = windowCaptureService;
        _microphoneRecorder = microphoneRecorder;
    }

    public async Task<CommandResponse> ExecuteAsync(CommandRequest request)
    {
        try
        {
            _logger.LogInformation("Executing action: {Action} with UUID: {Uuid}", request.Action, request.Uuid);

            return request.Action switch
            {
                "open_app" => await OpenApplication(request),
                "run_exe" => await RunExecutable(request),
                "search_files" => await SearchFiles(request),
                "adjust_setting" => await AdjustSetting(request),
                "system_status" => await GetSystemStatus(request),
                "create_folder" => await CreateFolder(request),
                "delete_folder" => await DeleteFolder(request),
                "move_file" => await MoveFile(request),
                "copy_file" => await CopyFile(request),
                "scan_applications" => await ScanApplications(request),
                "list_applications" => await ListApplications(request),
                "capture_window" => await CaptureWindow(request),
                "answer_question" => await AnswerQuestion(request),
                "show_desktop" => await ShowDesktop(request),
                "screenshot" => await TakeScreenshot(request),
                "mute" => await ToggleMute(request),
                "set_volume" => await SetVolume(request),
                "record_audio" => await RecordAudio(request),
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

    private async Task<CommandResponse> RecordAudio(CommandRequest request)
    {
        if (!request.Params.TryGetValue("duration", out var durationObj) || durationObj == null)
        {
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = "Missing required parameter: duration"
            };
        }

        if (!double.TryParse(durationObj.ToString(), out double durationSeconds))
        {
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = "Parameter 'duration' must be a number (seconds)"
            };
        }

        string? fileName = request.Params.TryGetValue("fileName", out var fileNameObj)
            ? fileNameObj?.ToString()
            : null;

        int sampleRate = 16_000;
        if (request.Params.TryGetValue("sampleRate", out var sampleRateObj) && sampleRateObj != null && int.TryParse(sampleRateObj.ToString(), out var parsedRate))
        {
            sampleRate = parsedRate;
        }

        int channels = 1;
        if (request.Params.TryGetValue("channels", out var channelsObj) && channelsObj != null && int.TryParse(channelsObj.ToString(), out var parsedChannels))
        {
            channels = parsedChannels;
        }

        try
        {
            var result = await _microphoneRecorder.RecordAsync(durationSeconds, fileName, sampleRate, channels);

            return new CommandResponse
            {
                Status = "ok",
                Result = new
                {
                    result.FileName,
                    result.Path,
                    result.DurationSeconds,
                    result.SampleRate,
                    result.Channels,
                    result.SizeBytes,
                    result.Format,
                    result.Base64Data,
                    capturedAt = result.CapturedAt.ToString("O")
                },
                Error = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record microphone audio");
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = $"Failed to record audio: {ex.Message}"
            };
        }
    }

    private async Task<CommandResponse> OpenApplication(CommandRequest request)
    {
        if (!request.Params.TryGetValue("application", out var appNameObj) || appNameObj == null)
        {
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = "Missing required parameter: application"
            };
        }

        var appName = appNameObj.ToString();
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

    private async Task<CommandResponse> RunExecutable(CommandRequest request)
    {
        if (!request.Params.TryGetValue("path", out var exePathObj) || exePathObj == null)
        {
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = "Missing required parameter: path"
            };
        }

        var exePath = exePathObj.ToString();
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = "Executable path is empty"
            };
        }

        try
        {
            _logger.LogInformation("Running executable: {Path}", exePath);

            // Expand environment variables and make absolute path
            var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(exePath));

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

            // Check if file exists
            if (!File.Exists(fullPath))
            {
                return new CommandResponse
                {
                    Status = "error",
                    Result = null,
                    Error = $"Executable not found: {fullPath}"
                };
            }

            // Verify it's an executable file
            var extension = Path.GetExtension(fullPath).ToLowerInvariant();
            if (extension != ".exe")
            {
                return new CommandResponse
                {
                    Status = "error",
                    Result = null,
                    Error = $"Invalid file type: {extension}. Only .exe files are allowed."
                };
            }

            // Build process start info
            var startInfo = new ProcessStartInfo
            {
                FileName = fullPath,
                UseShellExecute = true
            };

            var process = Process.Start(startInfo);

            if (process == null)
            {
                return new CommandResponse
                {
                    Status = "error",
                    Result = null,
                    Error = $"Failed to start executable: {fullPath}"
                };
            }

            await Task.Delay(500); // Give process time to start

            return new CommandResponse
            {
                Status = "ok",
                Result = new
                {
                    path = fullPath,
                    fileName = Path.GetFileName(fullPath),
                    processId = process.Id,
                    message = $"Successfully started {Path.GetFileName(fullPath)}"
                },
                Error = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run executable: {Path}", exePath);
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = $"Failed to run executable: {ex.Message}"
            };
        }
    }

    private async Task<CommandResponse> SearchFiles(CommandRequest request)
    {
        if (!request.Params.TryGetValue("query", out var queryObj) || queryObj == null)
        {
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = "Missing required parameter: query"
            };
        }

        var query = queryObj.ToString();
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
        if (!request.Params.TryGetValue("setting", out var settingObj) || settingObj == null)
        {
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = "Missing required parameter: setting"
            };
        }

        if (!request.Params.TryGetValue("value", out var valueObj) || valueObj == null)
        {
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = "Missing required parameter: value"
            };
        }

        var setting = settingObj.ToString();
        var value = valueObj.ToString();

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
        if (!request.Params.TryGetValue("path", out var pathObj) || pathObj == null)
        {
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = "Missing required parameter: path"
            };
        }

        var path = pathObj.ToString();
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
        if (!request.Params.TryGetValue("path", out var pathObj) || pathObj == null)
        {
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = "Missing required parameter: path"
            };
        }

        var path = pathObj.ToString();
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
        if (!request.Params.TryGetValue("source", out var sourceObj) || sourceObj == null)
        {
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = "Missing required parameter: source"
            };
        }

        if (!request.Params.TryGetValue("destination", out var destObj) || destObj == null)
        {
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = "Missing required parameter: destination"
            };
        }

        var source = sourceObj.ToString();
        var destination = destObj.ToString();

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
        if (!request.Params.TryGetValue("source", out var sourceObj) || sourceObj == null)
        {
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = "Missing required parameter: source"
            };
        }

        if (!request.Params.TryGetValue("destination", out var destObj) || destObj == null)
        {
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = "Missing required parameter: destination"
            };
        }

        var source = sourceObj.ToString();
        var destination = destObj.ToString();

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

    private async Task<CommandResponse> CaptureWindow(CommandRequest request)
    {
        if (!request.Params.TryGetValue("application", out var appNameObj) || appNameObj == null)
        {
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = "Missing required parameter: application"
            };
        }

        var applicationName = appNameObj.ToString();
        if (string.IsNullOrWhiteSpace(applicationName))
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
            _logger.LogInformation("Capturing window for application: {AppName}", applicationName);

            var captureResult = await _windowCaptureService.CaptureWindowAsync(applicationName);

            if (!captureResult.Success)
            {
                return new CommandResponse
                {
                    Status = "error",
                    Result = null,
                    Error = captureResult.Error
                };
            }

            return new CommandResponse
            {
                Status = "ok",
                Result = new
                {
                    application = captureResult.Application,
                    windowTitle = captureResult.WindowTitle,
                    processName = captureResult.ProcessName,
                    width = captureResult.Width,
                    height = captureResult.Height,
                    image = captureResult.ImageBase64,
                    capturedAt = captureResult.CapturedAt.ToString("O")
                },
                Error = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture window for application: {AppName}", applicationName);
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = $"Failed to capture window: {ex.Message}"
            };
        }
    }

    private async Task<CommandResponse> AnswerQuestion(CommandRequest request)
    {
        request.Params.TryGetValue("answer", out var answerRaw);
        request.Params.TryGetValue("question", out var questionRaw);

        var answerText = answerRaw?.ToString();
        if (string.IsNullOrWhiteSpace(answerText))
        {
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = "Answer text is empty"
            };
        }

        var questionText = questionRaw?.ToString();
        _logger.LogInformation("Answering question. Question: {Question}. Answer: {Answer}", questionText ?? "<not provided>", answerText);

        await SpeakTextAsync(answerText);

        return new CommandResponse
        {
            Status = "ok",
            Result = new
            {
                question = questionText,
                answer = answerText,
                spoken = OperatingSystem.IsWindows(),
                logged = true,
                message = "Answer delivered"
            },
            Error = null
        };
    }

    private async Task SpeakTextAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Cannot speak empty text");
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            _logger.LogInformation("Skipping speech synthesis because OS is not Windows");
            return;
        }

        try
        {
            // Sanitize text to prevent command injection
            // Remove any characters that could be used for injection
            var sanitizedText = SanitizeForSpeech(text);
            if (string.IsNullOrWhiteSpace(sanitizedText))
            {
                _logger.LogWarning("Text became empty after sanitization");
                return;
            }

            // Use base64 encoding to safely pass text to PowerShell
            var bytes = System.Text.Encoding.Unicode.GetBytes(sanitizedText);
            var base64Text = Convert.ToBase64String(bytes);

            // PowerShell script that decodes base64 and speaks
            var command = $"$text = [System.Text.Encoding]::Unicode.GetString([Convert]::FromBase64String('{base64Text}')); Add-Type -AssemblyName System.Speech; $synth = New-Object System.Speech.Synthesis.SpeechSynthesizer; $synth.Speak($text)";

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -Command \"{command}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                _logger.LogInformation("Speech synthesis finished with exit code {ExitCode}", process.ExitCode);
            }
            else
            {
                _logger.LogWarning("Failed to start speech synthesis process");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to synthesize speech for answer");
        }
    }

    /// <summary>
    /// Sanitize text for speech synthesis - removes potentially dangerous characters
    /// </summary>
    private static string SanitizeForSpeech(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        // Remove control characters and limit length
        var sanitized = new System.Text.StringBuilder();
        foreach (var c in text.Take(1000)) // Limit to 1000 chars
        {
            // Allow letters, digits, punctuation, and common whitespace
            if (char.IsLetterOrDigit(c) || char.IsPunctuation(c) || char.IsWhiteSpace(c))
            {
                sanitized.Append(c);
            }
        }
        return sanitized.ToString().Trim();
    }

    private async Task<CommandResponse> ShowDesktop(CommandRequest request)
    {
        try
        {
            _logger.LogInformation("Showing desktop (Win+D)");

            // Simulate Win+D keypress using PowerShell
            var script = @"
                $shell = New-Object -ComObject Shell.Application
                $shell.MinimizeAll()
            ";

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
            }

            return new CommandResponse
            {
                Status = "ok",
                Result = new
                {
                    action = "show_desktop",
                    message = "Desktop shown (all windows minimized)"
                },
                Error = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show desktop");
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = $"Failed to show desktop: {ex.Message}"
            };
        }
    }

    private async Task<CommandResponse> TakeScreenshot(CommandRequest request)
    {
        try
        {
            _logger.LogInformation("Taking full screen screenshot");

            // Get screen bounds using User32 API
            var screenWidth = Native.User32.GetSystemMetrics(0);  // SM_CXSCREEN
            var screenHeight = Native.User32.GetSystemMetrics(1); // SM_CYSCREEN

            if (screenWidth <= 0) screenWidth = 1920;
            if (screenHeight <= 0) screenHeight = 1080;

            using var bitmap = new System.Drawing.Bitmap(screenWidth, screenHeight);
            using var graphics = System.Drawing.Graphics.FromImage(bitmap);

            graphics.CopyFromScreen(0, 0, 0, 0, bitmap.Size);

            // Save to temp file
            var screenshotPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png"
            );

            bitmap.Save(screenshotPath, System.Drawing.Imaging.ImageFormat.Png);

            // Convert to base64
            using var ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            var base64 = Convert.ToBase64String(ms.ToArray());

            _logger.LogInformation("Screenshot saved to {Path}", screenshotPath);

            return new CommandResponse
            {
                Status = "ok",
                Result = new
                {
                    action = "screenshot",
                    path = screenshotPath,
                    width = screenWidth,
                    height = screenHeight,
                    image = base64,
                    message = $"Screenshot saved to {screenshotPath}"
                },
                Error = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to take screenshot");
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = $"Failed to take screenshot: {ex.Message}"
            };
        }
    }

    private async Task<CommandResponse> ToggleMute(CommandRequest request)
    {
        try
        {
            _logger.LogInformation("Toggling system mute");

            // Use nircmd or PowerShell to toggle mute
            var script = @"
                Add-Type -TypeDefinition @'
                using System.Runtime.InteropServices;
                public class Audio {
                    [DllImport(""user32.dll"")]
                    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);
                    public const byte VK_VOLUME_MUTE = 0xAD;
                }
'@
                [Audio]::keybd_event([Audio]::VK_VOLUME_MUTE, 0, 0, 0)
                [Audio]::keybd_event([Audio]::VK_VOLUME_MUTE, 0, 2, 0)
            ";

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
            }

            return new CommandResponse
            {
                Status = "ok",
                Result = new
                {
                    action = "mute",
                    message = "System mute toggled"
                },
                Error = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle mute");
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = $"Failed to toggle mute: {ex.Message}"
            };
        }
    }

    private async Task<CommandResponse> SetVolume(CommandRequest request)
    {
        try
        {
            var levelObj = request.Params.GetValueOrDefault("level");
            if (levelObj == null)
            {
                return new CommandResponse
                {
                    Status = "error",
                    Result = null,
                    Error = "Volume level is required"
                };
            }

            if (!int.TryParse(levelObj.ToString(), out int level) || level < 0 || level > 100)
            {
                return new CommandResponse
                {
                    Status = "error",
                    Result = null,
                    Error = "Volume level must be between 0 and 100"
                };
            }

            _logger.LogInformation("Setting volume to {Level}%", level);

            // Use PowerShell to set volume
            var script = $@"
                $obj = New-Object -ComObject WScript.Shell
                # First mute to reset, then set volume
                1..50 | ForEach-Object {{ $obj.SendKeys([char]174) }}
                1..{level / 2} | ForEach-Object {{ $obj.SendKeys([char]175) }}
            ";

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
            }

            return new CommandResponse
            {
                Status = "ok",
                Result = new
                {
                    action = "set_volume",
                    level = level,
                    message = $"Volume set to {level}%"
                },
                Error = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set volume");
            return new CommandResponse
            {
                Status = "error",
                Result = null,
                Error = $"Failed to set volume: {ex.Message}"
            };
        }
    }
}
