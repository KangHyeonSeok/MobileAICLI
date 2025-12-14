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
    private readonly IOptions<MobileAICLISettings> _options;

    public CopilotSessionServiceTests()
    {
        _mockLogger = new Mock<ILogger<CopilotSessionService>>();
        _settings = new MobileAICLISettings
        {
            CopilotInteractiveSessionTimeoutMinutes = 15,
            CopilotInteractiveMaxSessions = 20,
            CopilotInteractivePromptTimeoutSeconds = 3
        };
        _options = Options.Create(_settings);
    }

    public void Dispose()
    {
        _service?.Dispose();
    }

    [Fact]
    public async Task CreateSessionAsync_WithValidUserId_ReturnsSuccess()
    {
        // Arrange
        _service = new CopilotSessionService(_options, _mockLogger.Object);
        var userId = "test-user";

        // Act
        var result = await _service.CreateSessionAsync(userId);

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(result.SessionId);
        Assert.Empty(result.Error);
        Assert.Equal(0, _service.GetActiveSessionCount()); // Session not added to dictionary yet (placeholder implementation)
    }

    [Fact]
    public async Task CreateSessionAsync_WithNullUserId_ReturnsError()
    {
        // Arrange
        _service = new CopilotSessionService(_options, _mockLogger.Object);

        // Act
        var result = await _service.CreateSessionAsync(null!);

        // Assert
        Assert.False(result.Success);
        Assert.Empty(result.SessionId);
        Assert.Contains("User ID is required", result.Error);
    

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
        // Arrange
        _service = new CopilotSessionService(_options, _mockLogger.Object);

        // Act
        var result = await _service.CreateSessionAsync("");

        // Assert
        Assert.False(result.Success);
        Assert.Empty(result.SessionId);
        Assert.Contains("User ID is required", result.Error);
    }

    [Fact]
    public async Task CreateSessionAsync_RemovesExistingSessionForSameUser()
    {
        // Arrange
        _service = new CopilotSessionService(_options, _mockLogger.Object);
        var userId = "test-user";

        // Act - Create first session
        var result1 = await _service.CreateSessionAsync(userId);
        Assert.True(result1.Success);
        var sessionId1 = result1.SessionId;

        // Act - Create second session for same user
        var result2 = await _service.CreateSessionAsync(userId);
        Assert.True(result2.Success);
        var sessionId2 = result2.SessionId;

        // Assert - Sessions should be different
        Assert.NotEqual(sessionId1, sessionId2);
        
        // First session should no longer be accessible
        var session1 = _service.GetSession(userId, sessionId1);
        Assert.Null(session1);
    }

    [Fact]
    public async Task GetSession_WithValidUserIdAndSessionId_ReturnsNull_WhenNoSessionExists()
    {
        // Arrange
        _service = new CopilotSessionService(_options, _mockLogger.Object);
        var userId = "test-user";
        var result = await _service.CreateSessionAsync(userId);
        var sessionId = result.SessionId;

        // Act
        var session = _service.GetSession(userId, sessionId);

        // Assert
        // Since we haven't implemented actual session creation yet (Issue 3), 
        // the session won't be in the dictionary
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
        // Arrange
        _service = new CopilotSessionService(_options, _mockLogger.Object);
        var userId = "test-user";
        var wrongUserId = "wrong-user";
        var result = await _service.CreateSessionAsync(userId);
        var sessionId = result.SessionId;

        // Act
        var session = _service.GetSession(wrongUserId, sessionId);
        // Arrange - This test simulates the scenario, but won't actually create a session
        // because copilot command may not be available in test environment

        // Act
        var session = _service.GetSession("wrong-user", "some-session-id");

        // Assert
        Assert.Null(session);
    }

    [Fact]
    public void GetSession_WithNullUserId_ReturnsNull()
    {
        // Arrange
        _service = new CopilotSessionService(_options, _mockLogger.Object);

        // Act
        var session = _service.GetSession(null!, "some-session-id");

        // Assert
        Assert.Null(session);
    }

    [Fact]
    public void GetSession_WithNullSessionId_ReturnsNull()
    {
        // Arrange
        _service = new CopilotSessionService(_options, _mockLogger.Object);

        // Act
        var session = _service.GetSession("test-user", null!);

        // Assert
        Assert.Null(session);
    }

    [Fact]
    public async Task RemoveSessionAsync_WithValidUserIdAndSessionId_ReturnsTrue()
    {
        // Arrange
        _service = new CopilotSessionService(_options, _mockLogger.Object);
        var userId = "test-user";
        var result = await _service.CreateSessionAsync(userId);
        var sessionId = result.SessionId;

        // Act
        var removed = await _service.RemoveSessionAsync(userId, sessionId);

        // Assert
        Assert.True(removed);
        
        // Verify session is no longer accessible
        var session = _service.GetSession(userId, sessionId);
        Assert.Null(session);
    }

    [Fact]
    public async Task RemoveSessionAsync_WithWrongUserId_ReturnsFalse()
    {
        // Arrange
        _service = new CopilotSessionService(_options, _mockLogger.Object);
        var userId = "test-user";
        var wrongUserId = "wrong-user";
        var result = await _service.CreateSessionAsync(userId);
        var sessionId = result.SessionId;

        // Act
        var removed = await _service.RemoveSessionAsync(wrongUserId, sessionId);

        // Assert
        Assert.False(removed);
    }

    [Fact]
    public async Task RemoveSessionAsync_WithNullUserId_ReturnsFalse()
    {
        // Arrange
        _service = new CopilotSessionService(_options, _mockLogger.Object);

        // Act
        var removed = await _service.RemoveSessionAsync(null!, "some-session-id");

        // Assert
        Assert.False(removed);
    }

    [Fact]
    public async Task RemoveSessionAsync_WithNullSessionId_ReturnsFalse()
    {
        // Arrange
        _service = new CopilotSessionService(_options, _mockLogger.Object);

        // Act
        var removed = await _service.RemoveSessionAsync("test-user", null!);

        // Assert
        Assert.False(removed);
    }

    [Fact]
    public void GetActiveSessionCount_InitiallyReturnsZero()
    {
        // Arrange
        _service = new CopilotSessionService(_options, _mockLogger.Object);

        // Act
        var count = _service.GetActiveSessionCount();

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task CreateSessionAsync_WithMaxSessionsReached_StillCreatesSession()
    {
        // Arrange
        _settings.CopilotInteractiveMaxSessions = 2;
        var options = Options.Create(_settings);
        _service = new CopilotSessionService(options, _mockLogger.Object);

        // Act - Create 3 sessions with different users
        var result1 = await _service.CreateSessionAsync("user1");
        var result2 = await _service.CreateSessionAsync("user2");
        var result3 = await _service.CreateSessionAsync("user3");

        // Assert - All sessions should be created successfully
        // The service should handle cleanup internally
        Assert.True(result1.Success);
        Assert.True(result2.Success);
        Assert.True(result3.Success);
    }

    [Fact]
    public async Task CreateSessionAsync_GeneratesUniqueSessionIds()
    {
        // Arrange
        _service = new CopilotSessionService(_options, _mockLogger.Object);

        // Act
        var result1 = await _service.CreateSessionAsync("user1");
        var result2 = await _service.CreateSessionAsync("user2");
        var result3 = await _service.CreateSessionAsync("user3");

        // Assert
        Assert.NotEqual(result1.SessionId, result2.SessionId);
        Assert.NotEqual(result2.SessionId, result3.SessionId);
        Assert.NotEqual(result1.SessionId, result3.SessionId);
    }

    [Fact]
    public async Task Dispose_CleansUpAllSessions()
    {
        // Arrange
        _service = new CopilotSessionService(_options, _mockLogger.Object);
        await _service.CreateSessionAsync("user1");
        await _service.CreateSessionAsync("user2");

        // Act
        _service.Dispose();

        // Assert
        Assert.Equal(0, _service.GetActiveSessionCount());
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        _service = new CopilotSessionService(_options, _mockLogger.Object);

        // Act & Assert
        _service.Dispose();
        _service.Dispose(); // Should not throw
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
