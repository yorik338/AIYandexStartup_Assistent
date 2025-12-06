namespace JarvisCore.Validation;

/// <summary>
/// Result of command validation
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}
