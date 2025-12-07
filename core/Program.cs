using JarvisCore.Models;
using JarvisCore.Services;
using JarvisCore.Validation;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Listen on all interfaces so the Python bridge can reach the service regardless of
// whether it uses 127.0.0.1 or localhost.
builder.WebHost.UseUrls("http://0.0.0.0:5055");

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/jarvis-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Register services
builder.Services.AddSingleton<IActionExecutor, WindowsActionExecutor>();
builder.Services.AddSingleton<ICommandValidator, CommandValidator>();

var app = builder.Build();

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
        availableActions = new[] { "open_app", "search_files", "adjust_setting", "system_status" }
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
