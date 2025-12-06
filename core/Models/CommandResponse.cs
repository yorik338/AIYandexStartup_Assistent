namespace JarvisCore.Models;

/// <summary>
/// Response structure sent back to Python layer
/// </summary>
public class CommandResponse
{
    /// <summary>
    /// Status of the operation: "ok" or "error"
    /// </summary>
    public string Status { get; set; } = "ok";

    /// <summary>
    /// Result data (null if error occurred)
    /// </summary>
    public object? Result { get; set; }

    /// <summary>
    /// Error message (null if successful)
    /// </summary>
    public string? Error { get; set; }
}
