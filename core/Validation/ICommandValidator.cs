using JarvisCore.Models;

namespace JarvisCore.Validation;

/// <summary>
/// Interface for command validation
/// </summary>
public interface ICommandValidator
{
    ValidationResult Validate(CommandRequest request);
}
