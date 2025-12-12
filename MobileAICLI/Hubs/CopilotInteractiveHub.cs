using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using MobileAICLI.Models;
using MobileAICLI.Services;
using System.Security.Claims;

namespace MobileAICLI.Hubs;

/// <summary>
/// SignalR Hub for Interactive Copilot sessions.
/// Provides real-time communication for context-aware conversations with Copilot.
/// Phase 2: Interactive Mode - Issue 3
/// </summary>
public class CopilotInteractiveHub : Hub
{
    private readonly ICopilotSessionService _sessionService;
    private readonly MobileAICLISettings _settings;
    private readonly ILogger<CopilotInteractiveHub> _logger;

    public CopilotInteractiveHub(
        ICopilotSessionService sessionService,
        IOptions<MobileAICLISettings> settings,
        ILogger<CopilotInteractiveHub> logger)
    {
        _sessionService = sessionService;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Start a new interactive Copilot session.
    /// Client will receive SessionReady(sessionId) when ready.
    /// </summary>
    /// <returns>Task representing the async operation</returns>
    public async Task StartSession()
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("StartSession failed: User not authenticated");
            await Clients.Caller.SendAsync("ReceiveError", "User not authenticated", Context.ConnectionAborted);
            return;
        }

        _logger.LogInformation("StartSession called for user {UserId}", MaskUserId(userId));

        try
        {
            var (success, sessionId, error) = await _sessionService.CreateSessionAsync(userId, Context.ConnectionAborted);

            if (success)
            {
                _logger.LogInformation("Session created successfully: {SessionId} for user {UserId}", sessionId, MaskUserId(userId));
                await Clients.Caller.SendAsync("SessionReady", sessionId, Context.ConnectionAborted);
            }
            else
            {
                _logger.LogError("Failed to create session for user {UserId}: {Error}", MaskUserId(userId), error);
                await Clients.Caller.SendAsync("ReceiveError", error, Context.ConnectionAborted);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating session for user {UserId}", MaskUserId(userId));
            await Clients.Caller.SendAsync("ReceiveError", $"Failed to start session: {ex.Message}", Context.ConnectionAborted);
        }
    }

    /// <summary>
    /// Send a message to an active interactive session.
    /// Response is streamed back via ReceiveChunk and ReceiveComplete callbacks.
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <param name="prompt">User's message/prompt</param>
    /// <returns>Async enumerable for streaming response</returns>
    public async IAsyncEnumerable<string> SendMessage(string sessionId, string prompt)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("SendMessage failed: User not authenticated");
            await Clients.Caller.SendAsync("ReceiveError", "User not authenticated", Context.ConnectionAborted);
            yield break;
        }

        // Validate prompt length
        if (string.IsNullOrWhiteSpace(prompt))
        {
            _logger.LogWarning("SendMessage called with empty prompt for session {SessionId}", sessionId);
            await Clients.Caller.SendAsync("ReceiveError", "Prompt cannot be empty", Context.ConnectionAborted);
            yield break;
        }

        if (prompt.Length > _settings.CopilotInteractiveMaxPromptLength)
        {
            _logger.LogWarning("SendMessage called with prompt exceeding max length ({Length} > {MaxLength}) for session {SessionId}",
                prompt.Length, _settings.CopilotInteractiveMaxPromptLength, sessionId);
            await Clients.Caller.SendAsync("ReceiveError",
                $"Prompt exceeds maximum length of {_settings.CopilotInteractiveMaxPromptLength} characters",
                Context.ConnectionAborted);
            yield break;
        }

        _logger.LogInformation("SendMessage called for session {SessionId}, user {UserId}, prompt: {Prompt}",
            sessionId, MaskUserId(userId), TruncateForLog(prompt));

        // Get session
        var session = _sessionService.GetSession(userId, sessionId);
        if (session == null)
        {
            _logger.LogWarning("Session {SessionId} not found for user {UserId}", sessionId, MaskUserId(userId));
            await Clients.Caller.SendAsync("ReceiveError", "Session not found or expired. Please restart the session.", Context.ConnectionAborted);
            await Clients.Caller.SendAsync("ReceiveFallbackSuggestion",
                "Interactive mode session expired. Please try restarting the session or use Programmatic mode.",
                Context.ConnectionAborted);
            yield break;
        }

        if (!session.IsReady)
        {
            _logger.LogWarning("Session {SessionId} is not ready for user {UserId}", sessionId, MaskUserId(userId));
            await Clients.Caller.SendAsync("ReceiveError", "Session is not ready. Please wait or restart.", Context.ConnectionAborted);
            yield break;
        }

        // Stream the response using a helper method to avoid yield in try-catch
        await foreach (var chunk in StreamSessionResponseAsync(session, sessionId, userId, prompt, Context.ConnectionAborted))
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// Helper method to stream session response and handle errors.
    /// Separated from SendMessage to avoid yield in try-catch blocks.
    /// </summary>
    private async IAsyncEnumerable<string> StreamSessionResponseAsync(
        ICopilotInteractiveSession session,
        string sessionId,
        string userId,
        string prompt,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        bool hasError = false;
        Exception? capturedException = null;

        IAsyncEnumerator<string>? enumerator = null;
        try
        {
            // Write prompt to session
            try
            {
                await session.WriteAsync(prompt, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Session not ready when writing prompt for session {SessionId}", sessionId);
                await Clients.Caller.SendAsync("ReceiveError", "Session is not available or has been closed. Please restart the session.", cancellationToken);
                hasError = true;
                yield break;
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogWarning(ex, "Session disposed when writing prompt for session {SessionId}", sessionId);
                await Clients.Caller.SendAsync("ReceiveError", "Session has been disposed. Please restart the session.", cancellationToken);
                hasError = true;
                yield break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error when writing prompt for session {SessionId}", sessionId);
                await Clients.Caller.SendAsync("ReceiveError", "Unexpected error occurred when sending prompt to session.", cancellationToken);
                hasError = true;
                yield break;
            }

            // Get the enumerator
            enumerator = session.ReadResponseAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);

            // Stream response chunks
            while (true)
            {
                bool hasNext;
                string? current = null;

                try
                {
                    hasNext = await enumerator.MoveNextAsync();
                    if (hasNext)
                    {
                        current = enumerator.Current;
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("SendMessage cancelled for session {SessionId}", sessionId);
                    await Clients.Caller.SendAsync("ReceiveError", "Operation cancelled", cancellationToken);
                    hasError = true;
                    yield break;
                }
                catch (TimeoutException ex)
                {
                    _logger.LogWarning(ex, "Timeout in SendMessage for session {SessionId}", sessionId);
                    await Clients.Caller.SendAsync("ReceiveError", "Response timeout. The session may be unresponsive. Consider restarting.", cancellationToken);
                    await Clients.Caller.SendAsync("ReceiveFallbackSuggestion",
                        "The session timed out. You can try again or restart the session. Alternatively, use Programmatic mode.",
                        cancellationToken);
                    hasError = true;
                    yield break;
                }
                catch (Exception ex)
                {
                    capturedException = ex;
                    hasError = true;
                    break;
                }

                if (!hasNext)
                {
                    break;
                }

                yield return current!;
            }
        }
        finally
        {
            if (enumerator != null)
            {
                await enumerator.DisposeAsync();
            }
        }

        // Handle errors after yielding is complete
        if (hasError && capturedException != null)
        {
            _logger.LogError(capturedException, "Error in SendMessage for session {SessionId}: {Message}", sessionId, capturedException.Message);
            await Clients.Caller.SendAsync("ReceiveError", $"Error processing message: {capturedException.Message}", cancellationToken);
            
            // If process crashed, suggest fallback and clean up
            if (capturedException.Message.Contains("process", StringComparison.OrdinalIgnoreCase) ||
                capturedException.Message.Contains("exited", StringComparison.OrdinalIgnoreCase))
            {
                await Clients.Caller.SendAsync("ReceiveFallbackSuggestion",
                    "Interactive mode encountered an error. Please try Programmatic mode or restart the session.",
                    cancellationToken);
                
                try
                {
                    await _sessionService.RemoveSessionAsync(userId, sessionId);
                    _logger.LogInformation("Removed broken session {SessionId} for user {UserId}", sessionId, MaskUserId(userId));
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Failed to clean up broken session {SessionId}", sessionId);
                }
            }
        }
        else if (!hasError)
        {
            // Send completion signal only if no error occurred
            await Clients.Caller.SendAsync("ReceiveComplete", cancellationToken);
            _logger.LogInformation("SendMessage completed successfully for session {SessionId}", sessionId);
        }
    }

    /// <summary>
    /// End and dispose an active interactive session.
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <returns>Task representing the async operation</returns>
    public async Task EndSession(string sessionId)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("EndSession failed: User not authenticated");
            return;
        }

        _logger.LogInformation("EndSession called for session {SessionId}, user {UserId}", sessionId, MaskUserId(userId));

        try
        {
            var removed = await _sessionService.RemoveSessionAsync(userId, sessionId);
            if (removed)
            {
                _logger.LogInformation("Session {SessionId} ended successfully for user {UserId}", sessionId, MaskUserId(userId));
            }
            else
            {
                _logger.LogWarning("Session {SessionId} not found or already removed for user {UserId}", sessionId, MaskUserId(userId));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending session {SessionId} for user {UserId}", sessionId, MaskUserId(userId));
            await Clients.Caller.SendAsync("ReceiveError", $"Error ending session: {ex.Message}", Context.ConnectionAborted);
        }
    }

    /// <summary>
    /// Called when a client connects to the hub.
    /// Verifies that the user is authenticated.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var isAuthenticated = Context.User?.Identity?.IsAuthenticated ?? false;

        if (!isAuthenticated || string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Unauthenticated connection attempt from {ConnectionId}", Context.ConnectionId);
            Context.Abort();
            return;
        }

        _logger.LogInformation("Client connected: {ConnectionId}, User: {UserId}", Context.ConnectionId, MaskUserId(userId));
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// Cleans up any active sessions for this connection based on configured policy.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        
        if (exception != null)
        {
            _logger.LogWarning(exception, "Client disconnected with error: {ConnectionId}, User: {UserId}",
                Context.ConnectionId, string.IsNullOrEmpty(userId) ? "unknown" : MaskUserId(userId));
        }
        else
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}, User: {UserId}",
                Context.ConnectionId, string.IsNullOrEmpty(userId) ? "unknown" : MaskUserId(userId));
        }

        // Session cleanup is handled by the session service's background cleanup task
        // based on inactivity timeout. We don't immediately clean up on disconnect
        // to allow for reconnection scenarios.

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Extract user ID from the authentication context.
    /// Returns the user's name claim or empty string if not authenticated.
    /// </summary>
    private string GetUserId()
    {
        return Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;
    }

    /// <summary>
    /// Mask user ID for privacy in logs (shows first 2 characters only).
    /// </summary>
    private static string MaskUserId(string userId)
    {
        if (string.IsNullOrEmpty(userId) || userId.Length <= 2)
            return "***";
        
        return userId[..2] + "***";
    }

    /// <summary>
    /// Truncate text for logging to prevent excessive log sizes.
    /// </summary>
    private static string TruncateForLog(string text, int maxLength = 100)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        
        return text[..maxLength] + "...";
    }

    // Server → Client method signatures (for documentation)
    // These are called by the server and received by the client:
    // - SessionReady(string sessionId): Called when a new session is initialized and ready
    // - ReceiveChunk(string chunk): Called for each chunk of streaming response (via yield return)
    // - ReceiveComplete(): Called when response streaming is complete
    // - ReceiveError(string error): Called when an error occurs
    // - ReceiveFallbackSuggestion(string suggestion): Called to suggest fallback to Programmatic mode
}
