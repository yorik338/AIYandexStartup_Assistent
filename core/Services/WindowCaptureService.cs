using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using JarvisCore.Native;

namespace JarvisCore.Services;

/// <summary>
/// Service for capturing window screenshots without displaying on screen
/// </summary>
public class WindowCaptureService
{
    private readonly ILogger<WindowCaptureService> _logger;
    private readonly ApplicationRegistry _applicationRegistry;

    public WindowCaptureService(ILogger<WindowCaptureService> logger, ApplicationRegistry applicationRegistry)
    {
        _logger = logger;
        _applicationRegistry = applicationRegistry;
    }

    /// <summary>
    /// Captures a window screenshot by application name
    /// </summary>
    /// <param name="applicationName">Name or alias of the application</param>
    /// <returns>Capture result with image data</returns>
    public async Task<WindowCaptureResult> CaptureWindowAsync(string applicationName)
    {
        _logger.LogInformation("Capturing window for application: {AppName}", applicationName);

        // 1. Find the window handle
        var (hwnd, windowTitle, processName) = await FindWindowHandleAsync(applicationName);

        if (hwnd == IntPtr.Zero)
        {
            return new WindowCaptureResult
            {
                Success = false,
                Error = $"Could not find window for application: {applicationName}"
            };
        }

        _logger.LogInformation("Found window: {Title} (Handle: {Handle})", windowTitle, hwnd);

        // 2. Check if window is minimized
        bool wasMinimized = User32.IsIconic(hwnd);
        int originalExStyle = 0;

        try
        {
            if (wasMinimized)
            {
                _logger.LogInformation("Window is minimized, applying transparency trick");

                // Save original style and make window layered (transparent)
                originalExStyle = User32.GetWindowLong(hwnd, User32.GWL_EXSTYLE);
                User32.SetWindowLong(hwnd, User32.GWL_EXSTYLE, originalExStyle | User32.WS_EX_LAYERED);

                // Make completely transparent
                User32.SetLayeredWindowAttributes(hwnd, 0, 0, User32.LWA_ALPHA);

                // Restore window (it will be invisible due to transparency)
                User32.ShowWindowAsync(hwnd, User32.SW_RESTORE);

                // Wait for window to render
                await Task.Delay(150);
            }

            // 3. Get window dimensions
            User32.GetWindowRect(hwnd, out var rect);
            int width = rect.Width;
            int height = rect.Height;

            if (width <= 0 || height <= 0)
            {
                return new WindowCaptureResult
                {
                    Success = false,
                    Error = "Window has invalid dimensions"
                };
            }

            _logger.LogInformation("Window dimensions: {Width}x{Height}", width, height);

            // 4. Capture the window using PrintWindow
            byte[] imageData;
            using (var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    IntPtr hdc = graphics.GetHdc();

                    try
                    {
                        // PrintWindow with PW_RENDERFULLCONTENT for better capture
                        bool success = User32.PrintWindow(hwnd, hdc, User32.PW_RENDERFULLCONTENT);

                        if (!success)
                        {
                            // Fallback: try without flags
                            _logger.LogWarning("PrintWindow with PW_RENDERFULLCONTENT failed, trying without flags");
                            success = User32.PrintWindow(hwnd, hdc, 0);
                        }

                        if (!success)
                        {
                            return new WindowCaptureResult
                            {
                                Success = false,
                                Error = "PrintWindow failed to capture the window"
                            };
                        }
                    }
                    finally
                    {
                        graphics.ReleaseHdc(hdc);
                    }
                }

                // Convert to PNG byte array
                using (var ms = new MemoryStream())
                {
                    bitmap.Save(ms, ImageFormat.Png);
                    imageData = ms.ToArray();
                }
            }

            _logger.LogInformation("Successfully captured window, image size: {Size} bytes", imageData.Length);

            return new WindowCaptureResult
            {
                Success = true,
                Application = applicationName,
                WindowTitle = windowTitle,
                ProcessName = processName,
                Width = width,
                Height = height,
                ImageData = imageData,
                ImageBase64 = Convert.ToBase64String(imageData),
                CapturedAt = DateTime.UtcNow
            };
        }
        finally
        {
            // 5. Restore window state if it was minimized
            if (wasMinimized)
            {
                _logger.LogInformation("Restoring window to minimized state");

                // Minimize the window
                User32.ShowWindowAsync(hwnd, User32.SW_MINIMIZE);

                // Wait a bit before restoring style
                await Task.Delay(50);

                // Restore original window style (remove layered if we added it)
                User32.SetWindowLong(hwnd, User32.GWL_EXSTYLE, originalExStyle);
            }
        }
    }

    /// <summary>
    /// Finds window handle by application name
    /// </summary>
    private async Task<(IntPtr hwnd, string windowTitle, string processName)> FindWindowHandleAsync(string applicationName)
    {
        // First, try to find in application registry
        var appInfo = _applicationRegistry.FindApplication(applicationName);
        string? targetProcessName = null;

        if (appInfo != null)
        {
            targetProcessName = Path.GetFileNameWithoutExtension(appInfo.Path);
            _logger.LogInformation("Found in registry: {ProcessName} at {Path}", targetProcessName, appInfo.Path);
        }

        // Search for matching processes
        var processes = Process.GetProcesses();
        var candidates = new List<(IntPtr hwnd, string title, string procName, int score)>();

        foreach (var proc in processes)
        {
            try
            {
                if (proc.MainWindowHandle == IntPtr.Zero)
                    continue;

                string procName = proc.ProcessName;
                string windowTitle = GetWindowTitle(proc.MainWindowHandle);

                int score = 0;

                // Check if matches registry process name
                if (targetProcessName != null &&
                    procName.Equals(targetProcessName, StringComparison.OrdinalIgnoreCase))
                {
                    score += 100;
                }

                // Check if process name contains search term
                if (procName.Contains(applicationName, StringComparison.OrdinalIgnoreCase))
                {
                    score += 50;
                }

                // Check if window title contains search term
                if (windowTitle.Contains(applicationName, StringComparison.OrdinalIgnoreCase))
                {
                    score += 30;
                }

                // Check common aliases
                if (MatchesCommonAlias(applicationName, procName))
                {
                    score += 80;
                }

                if (score > 0)
                {
                    candidates.Add((proc.MainWindowHandle, windowTitle, procName, score));
                }
            }
            catch
            {
                // Skip inaccessible processes
            }
        }

        if (candidates.Count == 0)
        {
            _logger.LogWarning("No matching windows found for: {AppName}", applicationName);
            return (IntPtr.Zero, "", "");
        }

        // Return the best match
        var best = candidates.OrderByDescending(c => c.score).First();
        return (best.hwnd, best.title, best.procName);
    }

    private string GetWindowTitle(IntPtr hwnd)
    {
        int length = User32.GetWindowTextLength(hwnd);
        if (length == 0) return "";

        var sb = new StringBuilder(length + 1);
        User32.GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private bool MatchesCommonAlias(string searchTerm, string processName)
    {
        var aliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "telegram", new[] { "telegram" } },
            { "телеграм", new[] { "telegram" } },
            { "discord", new[] { "discord" } },
            { "дискорд", new[] { "discord" } },
            { "chrome", new[] { "chrome" } },
            { "хром", new[] { "chrome" } },
            { "firefox", new[] { "firefox" } },
            { "фаерфокс", new[] { "firefox" } },
            { "vscode", new[] { "code" } },
            { "vs code", new[] { "code" } },
            { "notepad", new[] { "notepad", "notepad++" } },
            { "блокнот", new[] { "notepad", "notepad++" } },
            { "explorer", new[] { "explorer" } },
            { "проводник", new[] { "explorer" } },
            { "steam", new[] { "steam", "steamwebhelper" } },
            { "стим", new[] { "steam", "steamwebhelper" } },
        };

        if (aliases.TryGetValue(searchTerm, out var possibleNames))
        {
            return possibleNames.Any(name =>
                processName.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }
}

/// <summary>
/// Result of window capture operation
/// </summary>
public class WindowCaptureResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Application { get; set; }
    public string? WindowTitle { get; set; }
    public string? ProcessName { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public byte[]? ImageData { get; set; }
    public string? ImageBase64 { get; set; }
    public DateTime CapturedAt { get; set; }
}
