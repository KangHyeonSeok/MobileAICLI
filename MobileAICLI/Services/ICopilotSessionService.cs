namespace MobileAICLI.Services;

/// <summary>
/// Manages multiple interactive Copilot sessions with lifecycle control and session pooling.
/// Phase 2: Interactive Mode - Issue 0
/// </summary>
public interface ICopilotSessionService
{
    /// <summary>
    /// Create a new interactive Copilot session for a user
    /// </summary>
    /// <param name="userId">User identifier from authentication context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple containing success status, session ID if successful, and error message</returns>
    Task<(bool Success, string SessionId, string Error)> CreateSessionAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve an existing session by user ID and session ID
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="sessionId">Session identifier</param>
    /// <returns>Session instance if found and owned by the user, null otherwise</returns>
    ICopilotInteractiveSession? GetSession(string userId, string sessionId);

    /// <summary>
    /// Remove and dispose a session
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="sessionId">Session identifier</param>
    /// <returns>Task representing the removal operation</returns>
    Task RemoveSessionAsync(string userId, string sessionId);

    /// <summary>
    /// Get the number of active sessions across all users
    /// </summary>
    /// <returns>Active session count</returns>
    int GetActiveSessionCount();
}
