using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MobileAICLI.Models;
using MobileAICLI.Services;
using Moq;
using Xunit;

namespace MobileAICLI.Tests.Services;

public class SettingsServiceTests
{
    private readonly Mock<IOptionsSnapshot<MobileAICLISettings>> _mockSettings;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<SettingsService>> _mockLogger;
    private readonly Mock<AuditLogService> _mockAuditLog;
    private readonly Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment> _mockEnv;

    public SettingsServiceTests()
    {
        _mockSettings = new Mock<IOptionsSnapshot<MobileAICLISettings>>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<SettingsService>>();
        _mockAuditLog = new Mock<AuditLogService>(Mock.Of<ILogger<AuditLogService>>());
        _mockEnv = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
        
        _mockEnv.Setup(e => e.ContentRootPath).Returns("/tmp");
    }

    [Fact]
    public void GetCurrentSettings_ReturnsSettings()
    {
        // Arrange
        var expectedSettings = new MobileAICLISettings
        {
            RepositoryPath = "/test/path",
            GitHubCopilotCommand = "copilot",
            GitCliPath = "git"
        };
        
        _mockSettings.Setup(s => s.Value).Returns(expectedSettings);
        
        var service = new SettingsService(
            _mockSettings.Object, 
            _mockConfiguration.Object, 
            _mockLogger.Object,
            _mockAuditLog.Object,
            _mockEnv.Object);

        // Act
        var result = service.GetCurrentSettings();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedSettings.RepositoryPath, result.RepositoryPath);
        Assert.Equal(expectedSettings.GitHubCopilotCommand, result.GitHubCopilotCommand);
        Assert.Equal(expectedSettings.GitCliPath, result.GitCliPath);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WithInvalidPath_ReturnsValidationError()
    {
        // Arrange
        var settings = new MobileAICLISettings();
        _mockSettings.Setup(s => s.Value).Returns(settings);
        
        var service = new SettingsService(
            _mockSettings.Object, 
            _mockConfiguration.Object, 
            _mockLogger.Object,
            _mockAuditLog.Object,
            _mockEnv.Object);

        var request = new SettingsUpdateRequest
        {
            RepositoryPath = "/nonexistent/path/that/does/not/exist"
        };

        // Act
        var result = await service.UpdateSettingsAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("does not exist", result.ValidationErrors[0]);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WithDangerousCommand_ReturnsValidationError()
    {
        // Arrange
        var settings = new MobileAICLISettings();
        _mockSettings.Setup(s => s.Value).Returns(settings);
        
        var service = new SettingsService(
            _mockSettings.Object, 
            _mockConfiguration.Object, 
            _mockLogger.Object,
            _mockAuditLog.Object,
            _mockEnv.Object);

        var request = new SettingsUpdateRequest
        {
            AllowedShellCommands = new List<string> { "rm -rf /" }
        };

        // Act
        var result = await service.UpdateSettingsAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Dangerous command", result.ValidationErrors[0]);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithEmptyPassword_ReturnsError()
    {
        // Arrange
        var settings = new MobileAICLISettings();
        _mockSettings.Setup(s => s.Value).Returns(settings);
        
        var service = new SettingsService(
            _mockSettings.Object, 
            _mockConfiguration.Object, 
            _mockLogger.Object,
            _mockAuditLog.Object,
            _mockEnv.Object);

        var request = new PasswordChangeRequest
        {
            CurrentPassword = "",
            NewPassword = ""
        };

        // Act
        var result = await service.ChangePasswordAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("required", result.Message);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithShortPassword_ReturnsError()
    {
        // Arrange
        var settings = new MobileAICLISettings();
        _mockSettings.Setup(s => s.Value).Returns(settings);
        
        var service = new SettingsService(
            _mockSettings.Object, 
            _mockConfiguration.Object, 
            _mockLogger.Object,
            _mockAuditLog.Object,
            _mockEnv.Object);

        var request = new PasswordChangeRequest
        {
            CurrentPassword = "oldpass",
            NewPassword = "short"
        };

        // Act
        var result = await service.ChangePasswordAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("at least 8 characters", result.Message);
    }
}
