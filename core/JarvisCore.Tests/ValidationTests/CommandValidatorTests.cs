using JarvisCore.Models;
using JarvisCore.Validation;
using FluentAssertions;
using Xunit;

namespace JarvisCore.Tests.ValidationTests;

public class CommandValidatorTests
{
    private readonly CommandValidator _validator;

    public CommandValidatorTests()
    {
        _validator = new CommandValidator();
    }

    [Fact]
    public void Validate_ValidOpenAppCommand_ShouldReturnValid()
    {
        // Arrange
        var request = new CommandRequest
        {
            Action = "open_app",
            Params = new Dictionary<string, object>
            {
                { "application", "notepad" }
            },
            Uuid = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow.ToString("O")
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ValidCreateFolderCommand_ShouldReturnValid()
    {
        // Arrange
        var request = new CommandRequest
        {
            Action = "create_folder",
            Params = new Dictionary<string, object>
            {
                { "path", @"C:\test" }
            },
            Uuid = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow.ToString("O")
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ValidMoveFileCommand_ShouldReturnValid()
    {
        // Arrange
        var request = new CommandRequest
        {
            Action = "move_file",
            Params = new Dictionary<string, object>
            {
                { "source", @"C:\test\file.txt" },
                { "destination", @"C:\test2\file.txt" }
            },
            Uuid = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow.ToString("O")
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ValidScanApplicationsCommand_ShouldReturnValid()
    {
        // Arrange
        var request = new CommandRequest
        {
            Action = "scan_applications",
            Params = new Dictionary<string, object>(),
            Uuid = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow.ToString("O")
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_EmptyAction_ShouldReturnInvalid()
    {
        // Arrange
        var request = new CommandRequest
        {
            Action = "",
            Params = new Dictionary<string, object>(),
            Uuid = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow.ToString("O")
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Action is required");
    }

    [Fact]
    public void Validate_UnknownAction_ShouldReturnInvalid()
    {
        // Arrange
        var request = new CommandRequest
        {
            Action = "unknown_action",
            Params = new Dictionary<string, object>(),
            Uuid = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow.ToString("O")
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainMatch("*is not allowed*");
    }

    [Fact]
    public void Validate_MissingRequiredParameter_ShouldReturnInvalid()
    {
        // Arrange
        var request = new CommandRequest
        {
            Action = "open_app",
            Params = new Dictionary<string, object>(), // Missing "application" parameter
            Uuid = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow.ToString("O")
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Missing required parameter: application");
    }

    [Fact]
    public void Validate_EmptyRequiredParameter_ShouldReturnInvalid()
    {
        // Arrange
        var request = new CommandRequest
        {
            Action = "open_app",
            Params = new Dictionary<string, object>
            {
                { "application", "" } // Empty value
            },
            Uuid = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow.ToString("O")
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Parameter 'application' cannot be empty");
    }

    [Fact]
    public void Validate_MissingUuid_ShouldReturnInvalid()
    {
        // Arrange
        var request = new CommandRequest
        {
            Action = "open_app",
            Params = new Dictionary<string, object>
            {
                { "application", "notepad" }
            },
            Uuid = "", // Empty UUID
            Timestamp = DateTime.UtcNow.ToString("O")
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("UUID is required");
    }

    [Fact]
    public void Validate_MissingTimestamp_ShouldReturnInvalid()
    {
        // Arrange
        var request = new CommandRequest
        {
            Action = "open_app",
            Params = new Dictionary<string, object>
            {
                { "application", "notepad" }
            },
            Uuid = Guid.NewGuid().ToString(),
            Timestamp = "" // Empty timestamp
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Timestamp is required");
    }

    [Fact]
    public void Validate_InvalidTimestampFormat_ShouldReturnInvalid()
    {
        // Arrange
        var request = new CommandRequest
        {
            Action = "open_app",
            Params = new Dictionary<string, object>
            {
                { "application", "notepad" }
            },
            Uuid = Guid.NewGuid().ToString(),
            Timestamp = "not-a-date" // Invalid format
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Timestamp must be in ISO 8601 format");
    }

    [Fact]
    public void Validate_MoveFile_MissingSourceParameter_ShouldReturnInvalid()
    {
        // Arrange
        var request = new CommandRequest
        {
            Action = "move_file",
            Params = new Dictionary<string, object>
            {
                { "destination", @"C:\test2\file.txt" } // Missing source
            },
            Uuid = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow.ToString("O")
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Missing required parameter: source");
    }

    [Fact]
    public void Validate_CopyFile_MissingDestinationParameter_ShouldReturnInvalid()
    {
        // Arrange
        var request = new CommandRequest
        {
            Action = "copy_file",
            Params = new Dictionary<string, object>
            {
                { "source", @"C:\test\file.txt" } // Missing destination
            },
            Uuid = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow.ToString("O")
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Missing required parameter: destination");
    }
}
