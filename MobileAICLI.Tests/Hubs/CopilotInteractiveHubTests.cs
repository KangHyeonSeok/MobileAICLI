using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MobileAICLI.Hubs;
using MobileAICLI.Models;
using MobileAICLI.Services;
using Moq;
using System.Security.Claims;
using Xunit;

namespace MobileAICLI.Tests.Hubs;

public class CopilotInteractiveHubTests
{
    private readonly Mock<ICopilotSessionService> _mockSessionService;
    private readonly Mock<IOptions<MobileAICLISettings>> _mockSettings;
    private readonly Mock<ILogger<CopilotInteractiveHub>> _mockLogger;
    private readonly MobileAICLISettings _settings;
    private readonly Mock<HubCallerContext> _mockContext;
    private readonly Mock<IHubCallerClients> _mockClients;
    private readonly Mock<ISingleClientProxy> _mockClientProxy;

    public CopilotInteractiveHubTests()
    {
        _mockSessionService = new Mock<ICopilotSessionService>();
        _mockSettings = new Mock<IOptions<MobileAICLISettings>>();
        _mockLogger = new Mock<ILogger<CopilotInteractiveHub>>();
        _mockContext = new Mock<HubCallerContext>();
        _mockClients = new Mock<IHubCallerClients>();
        _mockClientProxy = new Mock<ISingleClientProxy>();

        _settings = new MobileAICLISettings
        {
            CopilotInteractiveMaxPromptLength = 10000,
            CopilotInteractiveSessionTimeoutMinutes = 15,
            CopilotInteractiveMaxSessions = 20
        };

        _mockSettings.Setup(s => s.Value).Returns(_settings);
        _mockClients.Setup(c => c.Caller).Returns(_mockClientProxy.Object);
    }

    private CopilotInteractiveHub CreateHub(string? userName = "admin")
    {
        var hub = new CopilotInteractiveHub(_mockSessionService.Object, _mockSettings.Object, _mockLogger.Object);

        // Setup authenticated context
        var claims = new List<Claim>();
        if (userName != null)
        {
            claims.Add(new Claim(ClaimTypes.Name, userName));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _mockContext.Setup(c => c.User).Returns(principal);
        _mockContext.Setup(c => c.ConnectionId).Returns("test-connection-id");
        _mockContext.Setup(c => c.ConnectionAborted).Returns(CancellationToken.None);

        // Use reflection to set the Context property
        var contextProperty = typeof(Hub).GetProperty("Context");
        contextProperty?.SetValue(hub, _mockContext.Object);

        var clientsProperty = typeof(Hub).GetProperty("Clients");
        clientsProperty?.SetValue(hub, _mockClients.Object);

        return hub;
    }

    [Fact]
    public async Task StartSession_WithAuthenticatedUser_CreatesSession()
    {
        // Arrange
        var hub = CreateHub("admin");
        var expectedSessionId = "session-123";
        _mockSessionService
            .Setup(s => s.CreateSessionAsync("admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, expectedSessionId, string.Empty));

        // Act
        await hub.StartSession();

        // Assert
        _mockSessionService.Verify(s => s.CreateSessionAsync("admin", It.IsAny<CancellationToken>()), Times.Once);
        _mockClientProxy.Verify(c => c.SendCoreAsync("SessionReady", 
            It.Is<object[]>(o => o.Length == 1 && (string)o[0] == expectedSessionId), 
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartSession_WithUnauthenticatedUser_SendsError()
    {
        // Arrange
        var hub = CreateHub(null); // No username

        // Act
        await hub.StartSession();

        // Assert
        _mockSessionService.Verify(s => s.CreateSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockClientProxy.Verify(c => c.SendCoreAsync("ReceiveError",
            It.Is<object[]>(o => o.Length == 1 && o[0].ToString()!.Contains("not authenticated")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartSession_WhenServiceFails_SendsError()
    {
        // Arrange
        var hub = CreateHub("admin");
        var errorMessage = "Failed to create session";
        _mockSessionService
            .Setup(s => s.CreateSessionAsync("admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, string.Empty, errorMessage));

        // Act
        await hub.StartSession();

        // Assert
        _mockClientProxy.Verify(c => c.SendCoreAsync("ReceiveError",
            It.Is<object[]>(o => o.Length == 1 && (string)o[0] == errorMessage),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EndSession_WithValidSession_RemovesSession()
    {
        // Arrange
        var hub = CreateHub("admin");
        var sessionId = "session-123";
        _mockSessionService
            .Setup(s => s.RemoveSessionAsync("admin", sessionId))
            .ReturnsAsync(true);

        // Act
        await hub.EndSession(sessionId);

        // Assert
        _mockSessionService.Verify(s => s.RemoveSessionAsync("admin", sessionId), Times.Once);
    }

    [Fact]
    public async Task EndSession_WithUnauthenticatedUser_DoesNothing()
    {
        // Arrange
        var hub = CreateHub(null);
        var sessionId = "session-123";

        // Act
        await hub.EndSession(sessionId);

        // Assert
        _mockSessionService.Verify(s => s.RemoveSessionAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task OnConnectedAsync_WithAuthenticatedUser_Succeeds()
    {
        // Arrange
        var hub = CreateHub("admin");

        // Act
        await hub.OnConnectedAsync();

        // Assert - no exception thrown, connection not aborted
        _mockContext.Verify(c => c.Abort(), Times.Never);
    }

    [Fact]
    public async Task OnConnectedAsync_WithUnauthenticatedUser_AbortsConnection()
    {
        // Arrange
        var hub = CreateHub(null);

        // Act
        await hub.OnConnectedAsync();

        // Assert
        _mockContext.Verify(c => c.Abort(), Times.Once);
    }

    [Fact]
    public async Task OnDisconnectedAsync_LogsDisconnection()
    {
        // Arrange
        var hub = CreateHub("admin");

        // Act
        await hub.OnDisconnectedAsync(null);

        // Assert - verify logger was called (check via mock if needed)
        // This test mainly ensures no exceptions are thrown
    }

    [Fact]
    public async Task SendMessage_WithEmptyPrompt_SendsError()
    {
        // Arrange
        var hub = CreateHub("admin");
        var sessionId = "session-123";
        var emptyPrompt = "";

        // Act
        var result = new List<string>();
        await foreach (var chunk in hub.SendMessage(sessionId, emptyPrompt))
        {
            result.Add(chunk);
        }

        // Assert
        Assert.Empty(result);
        _mockClientProxy.Verify(c => c.SendCoreAsync("ReceiveError",
            It.Is<object[]>(o => o.Length == 1 && o[0].ToString()!.Contains("cannot be empty")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessage_WithTooLongPrompt_SendsError()
    {
        // Arrange
        var hub = CreateHub("admin");
        var sessionId = "session-123";
        var longPrompt = new string('a', 10001); // Exceeds max length

        // Act
        var result = new List<string>();
        await foreach (var chunk in hub.SendMessage(sessionId, longPrompt))
        {
            result.Add(chunk);
        }

        // Assert
        Assert.Empty(result);
        _mockClientProxy.Verify(c => c.SendCoreAsync("ReceiveError",
            It.Is<object[]>(o => o.Length == 1 && o[0].ToString()!.Contains("maximum length")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessage_WithNonExistentSession_SendsErrorAndFallback()
    {
        // Arrange
        var hub = CreateHub("admin");
        var sessionId = "non-existent-session";
        var prompt = "test prompt";

        _mockSessionService
            .Setup(s => s.GetSession("admin", sessionId))
            .Returns((ICopilotInteractiveSession?)null);

        // Act
        var result = new List<string>();
        await foreach (var chunk in hub.SendMessage(sessionId, prompt))
        {
            result.Add(chunk);
        }

        // Assert
        Assert.Empty(result);
        _mockClientProxy.Verify(c => c.SendCoreAsync("ReceiveError",
            It.Is<object[]>(o => o.Length == 1 && o[0].ToString()!.Contains("not found")),
            It.IsAny<CancellationToken>()), Times.Once);
        _mockClientProxy.Verify(c => c.SendCoreAsync("ReceiveFallbackSuggestion",
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessage_WithUnauthenticatedUser_SendsError()
    {
        // Arrange
        var hub = CreateHub(null);
        var sessionId = "session-123";
        var prompt = "test prompt";

        // Act
        var result = new List<string>();
        await foreach (var chunk in hub.SendMessage(sessionId, prompt))
        {
            result.Add(chunk);
        }

        // Assert
        Assert.Empty(result);
        _mockClientProxy.Verify(c => c.SendCoreAsync("ReceiveError",
            It.Is<object[]>(o => o.Length == 1 && o[0].ToString()!.Contains("not authenticated")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
