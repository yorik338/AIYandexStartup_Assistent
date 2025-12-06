namespace JarvisCore.Models;

/// <summary>
/// Incoming command structure from Python layer
/// </summary>
public class CommandRequest
{
    /// <summary>
    /// Type of action to execute (open_app, search_files, adjust_setting, system_status)
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Parameters for the action
    /// </summary>
    public Dictionary<string, object> Params { get; set; } = new();

    /// <summary>
    /// Unique identifier for this request
    /// </summary>
    public string Uuid { get; set; } = string.Empty;

    /// <summary>
    /// ISO 8601 timestamp of the request
    /// </summary>
    public string Timestamp { get; set; } = string.Empty;
}
