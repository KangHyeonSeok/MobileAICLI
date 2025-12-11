using Microsoft.AspNetCore.SignalR;
using MobileAICLI.Services;

namespace MobileAICLI.Hubs;

/// <summary>
/// SignalR Hub for Interactive Copilot sessions.
/// Provides real-time communication for context-aware conversations with Copilot.
/// Phase 2: Interactive Mode - Issue 0 (Skeleton)
/// </summary>
public class CopilotInteractiveHub : Hub
{
    private readonly ILogger<CopilotInteractiveHub> _logger;

    public CopilotInteractiveHub(ILogger<CopilotInteractiveHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Start a new interactive Copilot session.
    /// Client will receive SessionReady(sessionId) when ready.
    /// </summary>
    /// <returns>Task representing the async operation</returns>
    public async Task StartSession()
    {
        _logger.LogInformation("StartSession called - not yet implemented");
        await Task.CompletedTask;
        throw new NotImplementedException("StartSession will be implemented in Issue 1");
    }

    /// <summary>
    /// Send a message to an active interactive session.
    /// Response is streamed back via ReceiveChunk and ReceiveComplete callbacks.
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <param name="prompt">User's message/prompt</param>
    /// <returns>Async enumerable for streaming response (not yet implemented)</returns>
    public async IAsyncEnumerable<string> SendMessage(string sessionId, string prompt)
    {
        _logger.LogInformation("SendMessage called for session {SessionId} - not yet implemented", sessionId);
        await Task.CompletedTask;
        yield break;
        // Will be implemented in Issue 1
        // throw new NotImplementedException("SendMessage will be implemented in Issue 1");
    }

    /// <summary>
    /// End and dispose an active interactive session.
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <returns>Task representing the async operation</returns>
    public async Task EndSession(string sessionId)
    {
        _logger.LogInformation("EndSession called for session {SessionId} - not yet implemented", sessionId);
        await Task.CompletedTask;
        throw new NotImplementedException("EndSession will be implemented in Issue 1");
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
    /// Should cleanup any active sessions for this connection.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // Server → Client method signatures (for documentation)
    // These are called by the server and received by the client:
    // - ReceiveChunk(string chunk): Called for each chunk of streaming response
    // - ReceiveComplete(): Called when response streaming is complete
    // - ReceiveError(string error): Called when an error occurs
    // - SessionReady(string sessionId): Called when a new session is initialized and ready
}
