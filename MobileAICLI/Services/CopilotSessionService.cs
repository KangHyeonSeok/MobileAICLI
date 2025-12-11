using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using MobileAICLI.Models;

namespace MobileAICLI.Services;

/// <summary>
/// Manages multiple interactive Copilot sessions with lifecycle control and session pooling.
/// Implements user-based session isolation, automatic cleanup of inactive sessions, and resource limits.
/// Phase 2: Interactive Mode - Issue 2
/// </summary>
public class CopilotSessionService : ICopilotSessionService, IDisposable
{
    private readonly ConcurrentDictionary<string, ICopilotInteractiveSession> _sessions = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastActivityTime = new();
    private readonly ConcurrentDictionary<string, string> _sessionOwners = new();
    private readonly MobileAICLISettings _settings;
    private readonly ILogger<CopilotSessionService> _logger;
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    public CopilotSessionService(
        IOptions<MobileAICLISettings> settings,
        ILogger<CopilotSessionService> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        // Start background cleanup task (runs every minute)
        _cleanupTimer = new Timer(
            callback: _ => CleanupInactiveSessions(),
            state: null,
            dueTime: TimeSpan.FromMinutes(1),
            period: TimeSpan.FromMinutes(1));
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string SessionId, string Error)> CreateSessionAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("CreateSessionAsync called with null or empty userId");
            return (false, string.Empty, "User ID is required");
        }

        try
        {
            // Remove existing session for this user (one session per user rule)
            var existingSessions = _sessionOwners
                .Where(kvp => kvp.Value == userId)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var existingSessionId in existingSessions)
            {
                _logger.LogInformation("Removing existing session {SessionId} for user {UserId}", 
                    existingSessionId, userId);
                await RemoveSessionAsync(userId, existingSessionId);
            }

            // Check if we've reached the max session limit
            if (_sessions.Count >= _settings.CopilotInteractiveMaxSessions)
            {
                _logger.LogWarning("Maximum session limit ({MaxSessions}) reached. Cleaning up oldest sessions.",
                    _settings.CopilotInteractiveMaxSessions);
                
                // Remove oldest sessions to make room
                CleanupOldestSessions(1);
            }

            // Create new session
            // Note: This is a placeholder - actual session creation will be implemented in Issue 3
            // For now, we just track the session metadata
            var sessionId = Guid.NewGuid().ToString();
            
            _logger.LogInformation("Creating new interactive session {SessionId} for user {UserId}",
                sessionId, userId);

            // Track session ownership and activity
            _sessionOwners[sessionId] = userId;
            _lastActivityTime[sessionId] = DateTime.UtcNow;

            _logger.LogInformation("Successfully created session {SessionId} for user {UserId}. Active sessions: {Count}",
                sessionId, userId, _sessions.Count);

            return (true, sessionId, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create session for user {UserId}", userId);
            return (false, string.Empty, $"Failed to create session: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public ICopilotInteractiveSession? GetSession(string userId, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(sessionId))
        {
            _logger.LogWarning("GetSession called with null or empty userId or sessionId");
            return null;
        }

        // Verify session ownership
        if (!_sessionOwners.TryGetValue(sessionId, out var ownerId) || ownerId != userId)
        {
            _logger.LogWarning("User {UserId} attempted to access session {SessionId} owned by {OwnerId}",
                userId, sessionId, ownerId ?? "unknown");
            return null;
        }

        // Update last activity time
        _lastActivityTime[sessionId] = DateTime.UtcNow;

        // Return session if it exists
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            _logger.LogDebug("Retrieved session {SessionId} for user {UserId}", sessionId, userId);
            return session;
        }

        _logger.LogWarning("Session {SessionId} not found for user {UserId}", sessionId, userId);
        return null;
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveSessionAsync(string userId, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(sessionId))
        {
            _logger.LogWarning("RemoveSessionAsync called with null or empty userId or sessionId");
            return false;
        }

        // Verify session ownership
        if (!_sessionOwners.TryGetValue(sessionId, out var ownerId) || ownerId != userId)
        {
            _logger.LogWarning("User {UserId} attempted to remove session {SessionId} owned by {OwnerId}",
                userId, sessionId, ownerId ?? "unknown");
            return false;
        }

        try
        {
            _logger.LogInformation("Removing session {SessionId} for user {UserId}", sessionId, userId);

            // Remove session and dispose if it exists
            if (_sessions.TryRemove(sessionId, out var session))
            {
                session.Dispose();
            }

            // Remove tracking data
            _sessionOwners.TryRemove(sessionId, out _);
            _lastActivityTime.TryRemove(sessionId, out _);

            _logger.LogInformation("Successfully removed session {SessionId}. Active sessions: {Count}",
                sessionId, _sessions.Count);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing session {SessionId} for user {UserId}", sessionId, userId);
            return false;
        }
    }

    /// <inheritdoc/>
    public int GetActiveSessionCount()
    {
        return _sessions.Count;
    }

    /// <summary>
    /// Clean up inactive sessions that have exceeded the timeout period.
    /// Also enforces the maximum session limit by removing oldest sessions.
    /// This method is called periodically by a background timer.
    /// </summary>
    private void CleanupInactiveSessions()
    {
        try
        {
            var now = DateTime.UtcNow;
            var timeout = TimeSpan.FromMinutes(_settings.CopilotInteractiveSessionTimeoutMinutes);
            var sessionsToRemove = new List<string>();

            // Find inactive sessions
            foreach (var kvp in _lastActivityTime)
            {
                var sessionId = kvp.Key;
                var lastActivity = kvp.Value;

                if (now - lastActivity > timeout)
                {
                    sessionsToRemove.Add(sessionId);
                }
            }

            // Remove inactive sessions
            foreach (var sessionId in sessionsToRemove)
            {
                if (_sessionOwners.TryGetValue(sessionId, out var userId))
                {
                    _logger.LogInformation("Cleaning up inactive session {SessionId} for user {UserId} (last activity: {LastActivity})",
                        sessionId, userId, _lastActivityTime[sessionId]);
                    
                    // Use Task.Run to avoid blocking the timer thread
                    Task.Run(async () => await RemoveSessionAsync(userId, sessionId));
                }
            }

            // Enforce max session limit
            if (_sessions.Count > _settings.CopilotInteractiveMaxSessions)
            {
                var excess = _sessions.Count - _settings.CopilotInteractiveMaxSessions;
                _logger.LogWarning("Session count ({Count}) exceeds maximum ({Max}). Removing {Excess} oldest sessions.",
                    _sessions.Count, _settings.CopilotInteractiveMaxSessions, excess);
                CleanupOldestSessions(excess);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during session cleanup");
        }
    }

    /// <summary>
    /// Remove the specified number of oldest sessions based on last activity time
    /// </summary>
    private void CleanupOldestSessions(int count)
    {
        var oldestSessions = _lastActivityTime
            .OrderBy(kvp => kvp.Value)
            .Take(count)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var sessionId in oldestSessions)
        {
            if (_sessionOwners.TryGetValue(sessionId, out var userId))
            {
                _logger.LogInformation("Removing oldest session {SessionId} for user {UserId}",
                    sessionId, userId);
                
                // Use Task.Run to avoid blocking
                Task.Run(async () => await RemoveSessionAsync(userId, sessionId));
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Stop the cleanup timer
        _cleanupTimer?.Dispose();

        // Dispose all sessions
        foreach (var session in _sessions.Values)
        {
            try
            {
                session.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing session {SessionId}", session.SessionId);
            }
        }

        _sessions.Clear();
        _sessionOwners.Clear();
        _lastActivityTime.Clear();

        _logger.LogInformation("CopilotSessionService disposed");
    }
}
