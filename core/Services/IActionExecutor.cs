using JarvisCore.Models;

namespace JarvisCore.Services;

/// <summary>
/// Interface for executing Windows actions
/// </summary>
public interface IActionExecutor
{
    Task<CommandResponse> ExecuteAsync(CommandRequest request);
}
