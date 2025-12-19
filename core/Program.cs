using JarvisCore.Models;
using JarvisCore.Services;
using JarvisCore.Validation;
using Serilog;
using System.Collections.Concurrent;
using System.Text;

// Fix Cyrillic encoding in console
Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

// Simple rate limiter - 30 requests per second per IP
var requestCounts = new ConcurrentDictionary<string, (int count, DateTime resetTime)>();
const int MaxRequestsPerSecond = 30;

var builder = WebApplication.CreateBuilder(args);

// Listen on all interfaces so the Python bridge can reach the service regardless of
// whether it uses 127.0.0.1 or localhost.
builder.WebHost.UseUrls("http://localhost:5055");

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/jarvis-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Register services
builder.Services.AddSingleton<ApplicationScanner>();
builder.Services.AddSingleton<ApplicationRegistry>();
builder.Services.AddSingleton<WindowCaptureService>();
builder.Services.AddSingleton<IActionExecutor, WindowsActionExecutor>();
builder.Services.AddSingleton<ICommandValidator, CommandValidator>();

var app = builder.Build();

// Initialize ApplicationRegistry on startup (without automatic scan)
var appRegistry = app.Services.GetRequiredService<ApplicationRegistry>();
// Note: Registry will load from file if exists, or start empty
// Use scan_applications command to populate the registry when ready
try
{
    await appRegistry.InitializeAsync();
    Log.Information("Application registry initialized");
}
catch (Exception ex)
{
    Log.Warning(ex, "Could not initialize application registry, starting with empty registry");
}

// Rate limiting middleware
app.Use(async (context, next) =>
{
    var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var now = DateTime.UtcNow;

    var (count, resetTime) = requestCounts.GetOrAdd(clientIp, _ => (0, now.AddSeconds(1)));

    // Reset counter if time window passed
    if (now >= resetTime)
    {
        requestCounts[clientIp] = (1, now.AddSeconds(1));
        await next();
        return;
    }

    // Check rate limit
    if (count >= MaxRequestsPerSecond)
    {
        Log.Warning("Rate limit exceeded for IP: {ClientIp}", clientIp);
        context.Response.StatusCode = 429;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new CommandResponse
        {
            Status = "error",
            Result = null,
            Error = "Too many requests. Please slow down."
        });
        return;
    }

    // Increment counter
    requestCounts[clientIp] = (count + 1, resetTime);
    await next();
});

// Middleware for exception handling
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Unhandled exception occurred");
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new CommandResponse
        {
            Status = "error",
            Result = null,
            Error = "Internal server error"
        });
    }
});

// GET / - Welcome page
app.MapGet("/", () =>
{
    return Results.Ok(new
    {
        service = "JarvisCore",
        version = "1.0.0",
        status = "running",
        message = "JARVIS Assistant C# Core - Ready to serve",
        endpoints = new
        {
            execute = "POST /action/execute",
            status = "GET /system/status"
        },
        availableActions = new[]
        {
            "open_app", "run_exe", "search_files", "adjust_setting", "system_status",
            "create_folder", "delete_folder", "move_file", "copy_file",
            "scan_applications", "list_applications", "capture_window", "answer_question",
            "show_desktop", "screenshot", "mute", "set_volume"
        }
    });
});

// POST /action/execute - Execute command
app.MapPost("/action/execute", async (CommandRequest request, ICommandValidator validator, IActionExecutor executor) =>
{
    Log.Information("Received command: {Action} with UUID: {Uuid}", request.Action, request.Uuid);

    // Validate command
    var validationResult = validator.Validate(request);
    if (!validationResult.IsValid)
    {
        Log.Warning("Command validation failed: {Errors}", string.Join(", ", validationResult.Errors));
        return Results.Ok(new CommandResponse
        {
            Status = "error",
            Result = null,
            Error = $"Validation failed: {string.Join(", ", validationResult.Errors)}"
        });
    }

    // Execute action
    var result = await executor.ExecuteAsync(request);
    return Results.Ok(result);
});

// GET /system/status - Get system status
app.MapGet("/system/status", () =>
{
    Log.Information("System status check requested");
    return Results.Ok(new CommandResponse
    {
        Status = "ok",
        Result = new
        {
            service = "JarvisCore",
            version = "1.0.0",
            uptime = Environment.TickCount64,
            timestamp = DateTime.UtcNow.ToString("O")
        },
        Error = null
    });
});

Log.Information("Starting JarvisCore HTTP server on port 5055 (all interfaces)");
app.Run();
