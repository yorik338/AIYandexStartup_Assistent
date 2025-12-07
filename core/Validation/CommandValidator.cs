using JarvisCore.Models;
using System.Globalization;

namespace JarvisCore.Validation;

/// <summary>
/// Validates incoming commands against whitelist and required parameters
/// </summary>
public class CommandValidator : ICommandValidator
{
    // Whitelist of allowed actions with their required parameters
    private static readonly Dictionary<string, List<string>> AllowedActions = new()
    {
        { "open_app", new List<string> { "application" } },
        { "search_files", new List<string> { "query" } },
        { "adjust_setting", new List<string> { "setting", "value" } },
        { "system_status", new List<string>() },
        { "create_folder", new List<string> { "path" } },
        { "delete_folder", new List<string> { "path" } },
        { "move_file", new List<string> { "source", "destination" } },
        { "copy_file", new List<string> { "source", "destination" } }
    };

    public ValidationResult Validate(CommandRequest request)
    {
        var result = new ValidationResult { IsValid = true };

        // Validate action
        if (string.IsNullOrWhiteSpace(request.Action))
        {
            result.IsValid = false;
            result.Errors.Add("Action is required");
        }
        else if (!AllowedActions.ContainsKey(request.Action))
        {
            result.IsValid = false;
            result.Errors.Add($"Action '{request.Action}' is not allowed. Allowed actions: {string.Join(", ", AllowedActions.Keys)}");
        }
        else
        {
            // Validate required parameters for this action
            var requiredParams = AllowedActions[request.Action];
            foreach (var param in requiredParams)
            {
                if (!request.Params.ContainsKey(param))
                {
                    result.IsValid = false;
                    result.Errors.Add($"Missing required parameter: {param}");
                }
                else if (request.Params[param] == null || string.IsNullOrWhiteSpace(request.Params[param].ToString()))
                {
                    result.IsValid = false;
                    result.Errors.Add($"Parameter '{param}' cannot be empty");
                }
            }
        }

        // Validate UUID
        if (string.IsNullOrWhiteSpace(request.Uuid))
        {
            result.IsValid = false;
            result.Errors.Add("UUID is required");
        }

        // Validate timestamp (ISO 8601 format)
        if (string.IsNullOrWhiteSpace(request.Timestamp))
        {
            result.IsValid = false;
            result.Errors.Add("Timestamp is required");
        }
        else if (!DateTime.TryParse(request.Timestamp, null, DateTimeStyles.RoundtripKind, out _))
        {
            result.IsValid = false;
            result.Errors.Add("Timestamp must be in ISO 8601 format");
        }

        return result;
    }
}
