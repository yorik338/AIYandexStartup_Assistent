using JarvisCore.Security;
using FluentAssertions;
using Xunit;

namespace JarvisCore.Tests.ValidationTests;

public class PathValidatorTests
{
    [Theory]
    [InlineData(@"C:\Users\TestUser\Documents\test.txt")]
    [InlineData(@"C:\Temp\folder")]
    [InlineData(@"D:\Projects\MyProject")]
    public void IsValidPath_ValidUserPaths_ShouldReturnTrue(string path)
    {
        // Act
        var result = PathValidator.IsValidPath(path);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(@"C:\Windows")]
    [InlineData(@"C:\Windows\System32")]
    [InlineData(@"C:\Program Files\Windows Defender")]
    public void IsValidPath_SystemPaths_ShouldReturnFalse(string path)
    {
        // Act
        var result = PathValidator.IsValidPath(path);

        // Assert
        result.Should().BeFalse($"System path {path} should not be allowed");
    }

    [Theory]
    [InlineData(@"C:\Program Files")]
    [InlineData(@"C:\Program Files (x86)")]
    public void IsValidPath_ProgramFilesPaths_ShouldReturnFalse(string path)
    {
        // Act
        var result = PathValidator.IsValidPath(path);

        // Assert
        result.Should().BeFalse($"Program Files path {path} should not be allowed");
    }

    [Theory]
    [InlineData(@"%USERPROFILE%\Documents\test.txt")]
    [InlineData(@"%TEMP%\tempfile.txt")]
    [InlineData(@"%APPDATA%\MyApp")]
    public void ExpandPath_EnvironmentVariables_ShouldExpand(string path)
    {
        // Act
        var result = PathValidator.ExpandPath(path);

        // Assert
        result.Should().NotContain("%");
        result.Should().NotBe(path);
    }

    [Theory]
    [InlineData(@"C:\Users\Test\file.txt")]
    [InlineData(@"D:\folder\subfolder")]
    public void ExpandPath_RegularPaths_ShouldReturnUnchanged(string path)
    {
        // Act
        var result = PathValidator.ExpandPath(path);

        // Assert
        result.Should().Be(path);
    }

    [Fact]
    public void IsValidPath_NullPath_ShouldReturnFalse()
    {
        // Act
        var result = PathValidator.IsValidPath(null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidPath_EmptyPath_ShouldReturnFalse()
    {
        // Act
        var result = PathValidator.IsValidPath("");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidPath_WhitespacePath_ShouldReturnFalse()
    {
        // Act
        var result = PathValidator.IsValidPath("   ");

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(@"C:\")]
    [InlineData(@"D:\")]
    public void IsValidPath_RootDrive_ShouldReturnFalse(string path)
    {
        // Act
        var result = PathValidator.IsValidPath(path);

        // Assert
        result.Should().BeFalse($"Root drive {path} should not be allowed");
    }
}
