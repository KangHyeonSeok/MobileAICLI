using Microsoft.AspNetCore.SignalR;
using MobileAICLI.Services;
using System.Security.Claims;

namespace MobileAICLI.Hubs;

/// <summary>
/// SignalR Hub for Interactive Copilot sessions.
/// Provides real-time communication for context-aware conversations with Copilot.
/// Phase 2: Interactive Mode - Issue 1
/// </summary>
public class CopilotInteractiveHub : Hub
{
    private readonly ILogger<CopilotInteractiveHub> _logger;
    private readonly ICopilotSessionService _sessionService;

    public CopilotInteractiveHub(
        ILogger<CopilotInteractiveHub> logger,
        ICopilotSessionService sessionService)
    {
        _logger = logger;
        _sessionService = sessionService;
    }

    /// <summary>
    /// Get user identifier from the current context
    /// </summary>
    private string GetUserId()
    {
        // Try to get user ID from authentication claims
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? Context.User?.FindFirst("sub")?.Value
                  ?? Context.UserIdentifier
                  ?? Context.ConnectionId; // Fallback to connection ID

        return userId;
    }

    /// <summary>
    /// Start a new interactive Copilot session.
    /// Client will receive SessionReady(sessionId) when ready.
    /// </summary>
    /// <returns>Task representing the async operation</returns>
    public async Task StartSession()
    {
        var userId = GetUserId();
        _logger.LogInformation("StartSession called for user {UserId}", userId);

        try
        {
            var (success, sessionId, error) = await _sessionService.CreateSessionAsync(userId);

            if (success)
            {
                _logger.LogInformation("Session {SessionId} created for user {UserId}", sessionId, userId);
                await Clients.Caller.SendAsync("SessionReady", sessionId);
            }
            else
            {
                _logger.LogWarning("Failed to create session for user {UserId}: {Error}", userId, error);
                await Clients.Caller.SendAsync("ReceiveError", error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting session for user {UserId}", userId);
            await Clients.Caller.SendAsync("ReceiveError", $"Failed to start session: {ex.Message}");
        }
    }

    /// <summary>
    /// Send a message to an active interactive session.
    /// Response is streamed back chunk by chunk.
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <param name="prompt">User's message/prompt</param>
    /// <returns>Async enumerable for streaming response chunks</returns>
    public async IAsyncEnumerable<string> SendMessage(string sessionId, string prompt)
    {
        _logger.LogInformation("SendMessage called for session {SessionId} - not yet implemented", sessionId);
        await Task.CompletedTask;
        throw new NotImplementedException("SendMessage will be implemented in Issue 1");
        var userId = GetUserId();
        _logger.LogInformation("SendMessage called for session {SessionId} by user {UserId}", sessionId, userId);

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            _logger.LogWarning("SendMessage called with empty session ID");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            _logger.LogWarning("SendMessage called with empty prompt");
            yield break;
        }

        var session = _sessionService.GetSession(userId, sessionId);
        if (session == null)
        {
            _logger.LogWarning("Session {SessionId} not found for user {UserId}", sessionId, userId);
            yield break;
        }

        // Write the prompt to the session (outside of iteration)
        await session.WriteAsync(prompt);

        // Stream the response back
        await foreach (var chunk in session.ReadResponseAsync())
        {
            if (!string.IsNullOrEmpty(chunk))
            {
                yield return chunk;
            }
        }

        _logger.LogDebug("Message processing complete for session {SessionId}", sessionId);
    }

    /// <summary>
    /// End and dispose an active interactive session.
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <returns>Task representing the async operation</returns>
    public async Task EndSession(string sessionId)
    {
        var userId = GetUserId();
        _logger.LogInformation("EndSession called for session {SessionId} by user {UserId}", sessionId, userId);

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            _logger.LogWarning("EndSession called with empty session ID");
            return;
        }

        try
        {
            var removed = await _sessionService.RemoveSessionAsync(userId, sessionId);
            if (removed)
            {
                _logger.LogInformation("Session {SessionId} ended successfully", sessionId);
            }
            else
            {
                _logger.LogWarning("Failed to end session {SessionId} - not found or not owned by user", sessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending session {SessionId}", sessionId);
        }
    }

    /// <summary>
    /// Called when a client connects to the hub.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// Cleanup any active sessions for this connection.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        _logger.LogInformation("Client disconnected: {ConnectionId}, UserId: {UserId}", Context.ConnectionId, userId);

        // Note: Sessions are cleaned up by the background cleanup task in CopilotSessionService
        // We don't immediately remove sessions on disconnect to allow reconnection scenarios
        
        await base.OnDisconnectedAsync(exception);
    }

    // Server → Client method signatures (for documentation)
    // These are called by the server and received by the client:
    // - ReceiveChunk(string chunk): Called for each chunk of streaming response
    // - ReceiveComplete(): Called when response streaming is complete
    // - ReceiveError(string error): Called when an error occurs
    // - SessionReady(string sessionId): Called when a new session is initialized and ready
}
