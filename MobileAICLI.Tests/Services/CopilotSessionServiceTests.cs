using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MobileAICLI.Models;
using MobileAICLI.Services;
using Moq;
using Xunit;

namespace MobileAICLI.Tests.Services;

public class CopilotSessionServiceTests : IDisposable
{
    private readonly Mock<IOptions<MobileAICLISettings>> _mockSettings;
    private readonly Mock<ILogger<CopilotSessionService>> _mockLogger;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly MobileAICLISettings _settings;
    private readonly CopilotSessionService _service;

    public CopilotSessionServiceTests()
    {
        _settings = new MobileAICLISettings
        {
            RepositoryPath = "/tmp",
            CopilotInteractiveSessionTimeoutMinutes = 15,
            CopilotInteractiveMaxSessions = 20,
            CopilotInteractivePromptPattern = @">\s?$",
            CopilotInteractivePromptTimeoutSeconds = 3,
            CopilotInteractiveMaxPromptLength = 10000
        };

        _mockSettings = new Mock<IOptions<MobileAICLISettings>>();
        _mockSettings.Setup(s => s.Value).Returns(_settings);

        _mockLogger = new Mock<ILogger<CopilotSessionService>>();

        // Setup service provider to return mocked dependencies
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(ILogger<CopilotInteractiveSession>)))
            .Returns(Mock.Of<ILogger<CopilotInteractiveSession>>());
        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(IOptions<MobileAICLISettings>)))
            .Returns(_mockSettings.Object);

        _service = new CopilotSessionService(
            _mockSettings.Object,
            _mockLogger.Object,
            _mockServiceProvider.Object
        );
    }

    [Fact]
    public void GetActiveSessionCount_InitiallyZero()
    {
        // Act
        var count = _service.GetActiveSessionCount();

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task CreateSessionAsync_WithEmptyUserId_ReturnsError()
    {
        // Act
        var result = await _service.CreateSessionAsync("");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("User ID is required", result.Error);
    }

    [Fact]
    public async Task GetSession_WithInvalidSessionId_ReturnsNull()
    {
        // Act
        var session = _service.GetSession("user1", "invalid-session-id");

        // Assert
        Assert.Null(session);
    }

    [Fact]
    public async Task GetSession_WithWrongUserId_ReturnsNull()
    {
        // Arrange - This test simulates the scenario, but won't actually create a session
        // because copilot command may not be available in test environment

        // Act
        var session = _service.GetSession("wrong-user", "some-session-id");

        // Assert
        Assert.Null(session);
    }

    [Fact]
    public async Task RemoveSessionAsync_WithNonExistentSession_ReturnsFalse()
    {
        // Act
        var result = await _service.RemoveSessionAsync("user1", "non-existent-session");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task RemoveSessionAsync_WithEmptyUserId_ReturnsFalse()
    {
        // Act
        var result = await _service.RemoveSessionAsync("", "some-session-id");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task RemoveSessionAsync_WithEmptySessionId_ReturnsFalse()
    {
        // Act
        var result = await _service.RemoveSessionAsync("user1", "");

        // Assert
        Assert.False(result);
    }

    public void Dispose()
    {
        _service?.Dispose();
    }
}
