using JarvisCore.Models;
using JarvisCore.Services;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using Xunit;

namespace JarvisCore.Tests.ServicesTests;

public class ApplicationRegistryTests
{
    private readonly Mock<ILogger<ApplicationRegistry>> _loggerMock;
    private readonly Mock<ApplicationScanner> _scannerMock;
    private readonly ApplicationRegistry _registry;

    public ApplicationRegistryTests()
    {
        _loggerMock = new Mock<ILogger<ApplicationRegistry>>();
        var scannerLoggerMock = new Mock<ILogger<ApplicationScanner>>();
        _scannerMock = new Mock<ApplicationScanner>(scannerLoggerMock.Object);

        // Setup scanner to return system apps
        _scannerMock.Setup(s => s.GetSystemApplications())
            .Returns(new List<ApplicationInfo>
            {
                new ApplicationInfo
                {
                    Id = "system_notepad",
                    Name = "Notepad",
                    Path = "notepad.exe",
                    Category = "System",
                    IsSystemApp = true,
                    Aliases = new List<string> { "notepad", "блокнот" }
                }
            });

        _registry = new ApplicationRegistry(_loggerMock.Object, _scannerMock.Object);
    }

    [Fact]
    public void FindApplication_ByExactName_ShouldReturnApp()
    {
        // Arrange
        _registry.InitializeAsync().Wait();

        // Act
        var result = _registry.FindApplication("notepad");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Notepad");
        result.ExecutableName.Should().Be("notepad.exe");
    }

    [Fact]
    public void FindApplication_ByAlias_ShouldReturnApp()
    {
        // Arrange
        _registry.InitializeAsync().Wait();

        // Act
        var result = _registry.FindApplication("блокнот");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Notepad");
    }

    [Fact]
    public void FindApplication_CaseInsensitive_ShouldReturnApp()
    {
        // Arrange
        _registry.InitializeAsync().Wait();

        // Act
        var result = _registry.FindApplication("NOTEPAD");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Notepad");
    }

    [Fact]
    public void FindApplication_NonExistentApp_ShouldReturnNull()
    {
        // Arrange
        _registry.InitializeAsync().Wait();

        // Act
        var result = _registry.FindApplication("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FindApplication_NullOrEmpty_ShouldReturnNull()
    {
        // Arrange
        _registry.InitializeAsync().Wait();

        // Act
        var result1 = _registry.FindApplication(null!);
        var result2 = _registry.FindApplication("");
        var result3 = _registry.FindApplication("   ");

        // Assert
        result1.Should().BeNull();
        result2.Should().BeNull();
        result3.Should().BeNull();
    }

    [Fact]
    public void GetAllApplications_ShouldReturnList()
    {
        // Arrange
        _registry.InitializeAsync().Wait();

        // Act
        var result = _registry.GetAllApplications();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThan(0);
        result.Should().Contain(a => a.Name == "Notepad");
    }

    [Fact]
    public void GetApplicationsByCategory_System_ShouldReturnSystemApps()
    {
        // Arrange
        _registry.InitializeAsync().Wait();

        // Act
        var result = _registry.GetApplicationsByCategory("System");

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThan(0);
        result.Should().OnlyContain(a => a.Category == "System");
    }

    [Fact]
    public void GetApplicationsByCategory_NonExistent_ShouldReturnEmpty()
    {
        // Arrange
        _registry.InitializeAsync().Wait();

        // Act
        var result = _registry.GetApplicationsByCategory("NonExistent");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetStatistics_ShouldReturnCorrectStats()
    {
        // Arrange
        _registry.InitializeAsync().Wait();

        // Act
        var stats = _registry.GetStatistics();

        // Assert
        stats.Should().NotBeNull();

        var totalApps = ((dynamic)stats).TotalApplications;
        totalApps.Should().BeGreaterThan(0);

        var systemApps = ((dynamic)stats).SystemApplications;
        systemApps.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ScanAndSaveAsync_ShouldMergeApplications()
    {
        // Arrange
        await _registry.InitializeAsync();

        var newApp = new ApplicationInfo
        {
            Id = "app_chrome",
            Name = "Google Chrome",
            Path = @"C:\Program Files\Google\Chrome\chrome.exe",
            ExecutableName = "chrome.exe",
            Category = "Browser",
            IsSystemApp = false,
            Aliases = new List<string> { "chrome" }
        };

        _scannerMock.Setup(s => s.ScanApplicationsAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<ApplicationInfo>
            {
                new ApplicationInfo
                {
                    Id = "system_notepad",
                    Name = "Notepad",
                    Path = "notepad.exe",
                    Category = "System",
                    IsSystemApp = true,
                    Aliases = new List<string> { "notepad" }
                },
                newApp
            });

        // Act
        await _registry.ScanAndSaveAsync();
        var allApps = _registry.GetAllApplications();

        // Assert
        allApps.Should().Contain(a => a.Name == "Google Chrome");
        allApps.Should().Contain(a => a.Name == "Notepad");
    }
}
