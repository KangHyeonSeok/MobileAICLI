using Microsoft.Extensions.Options;
using MobileAICLI.Models;
using System.Collections.Concurrent;

namespace MobileAICLI.Services;

/// <summary>
/// Manages multiple interactive Copilot sessions with lifecycle control.
/// Implements session pooling, timeout management, and user-based isolation.
/// </summary>
public class CopilotSessionService : ICopilotSessionService, IDisposable
{
    private readonly MobileAICLISettings _settings;
    private readonly ILogger<CopilotSessionService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, SessionMetadata> _sessions = new();
    private readonly ConcurrentDictionary<string, string> _userToSession = new();
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    private class SessionMetadata
    {
        public required ICopilotInteractiveSession Session { get; set; }
        public required string UserId { get; set; }
        public DateTime LastActivityTime { get; set; }
    }

    public CopilotSessionService(
        IOptions<MobileAICLISettings> settings,
        ILogger<CopilotSessionService> logger,
        IServiceProvider serviceProvider)
    {
        _settings = settings.Value;
        _logger = logger;
        _serviceProvider = serviceProvider;

        // Start cleanup timer (runs every minute)
        _cleanupTimer = new Timer(CleanupInactiveSessions, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// Create a new interactive session for a user
    /// </summary>
    public async Task<(bool Success, string SessionId, string Error)> CreateSessionAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return (false, string.Empty, "User ID is required");
        }

        try
        {
            // Check if user already has a session - remove it first
            if (_userToSession.TryGetValue(userId, out var existingSessionId))
            {
                _logger.LogInformation("User {UserId} already has session {SessionId}, removing old session", userId, existingSessionId);
                await RemoveSessionAsync(userId, existingSessionId);
            }

            // Check global session limit
            if (_sessions.Count >= _settings.CopilotInteractiveMaxSessions)
            {
                _logger.LogWarning("Maximum session limit reached ({MaxSessions}), attempting cleanup", _settings.CopilotInteractiveMaxSessions);
                
                // Try to cleanup inactive sessions
                CleanupInactiveSessions(null);

                // Check again after cleanup
                if (_sessions.Count >= _settings.CopilotInteractiveMaxSessions)
                {
                    // Remove oldest session
                    var oldestSession = _sessions
                        .OrderBy(s => s.Value.LastActivityTime)
                        .FirstOrDefault();

                    if (oldestSession.Value != null)
                    {
                        _logger.LogInformation("Removing oldest session {SessionId} to make room", oldestSession.Key);
                        await RemoveSessionAsync(oldestSession.Value.UserId, oldestSession.Key);
                    }
                }
            }

            // Create new session
            var sessionId = Guid.NewGuid().ToString("N");
            
            // Create session instance using DI
            var logger = _serviceProvider.GetRequiredService<ILogger<CopilotInteractiveSession>>();
            var settings = _serviceProvider.GetRequiredService<IOptions<MobileAICLISettings>>();
            var session = new CopilotInteractiveSession(sessionId, settings, logger);

            // Initialize the session
            var (success, error) = await session.InitializeAsync(cancellationToken);
            
            if (!success)
            {
                session.Dispose();
                _logger.LogError("Failed to initialize session {SessionId} for user {UserId}: {Error}", sessionId, userId, error);
                return (false, string.Empty, error);
            }

            // Store session metadata
            var metadata = new SessionMetadata
            {
                Session = session,
                UserId = userId,
                LastActivityTime = DateTime.UtcNow
            };

            if (!_sessions.TryAdd(sessionId, metadata))
            {
                session.Dispose();
                return (false, string.Empty, "Failed to register session");
            }

            // Map user to session
            _userToSession[userId] = sessionId;

            _logger.LogInformation("Created new interactive session {SessionId} for user {UserId}", sessionId, userId);
            return (true, sessionId, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating session for user {UserId}", userId);
            return (false, string.Empty, $"Failed to create session: {ex.Message}");
        }
    }

    /// <summary>
    /// Retrieve an existing session by user ID and session ID
    /// </summary>
    public ICopilotInteractiveSession? GetSession(string userId, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        if (!_sessions.TryGetValue(sessionId, out var metadata))
        {
            return null;
        }

        // Verify user owns this session
        if (metadata.UserId != userId)
        {
            _logger.LogWarning("User {UserId} attempted to access session {SessionId} owned by {OwnerId}", 
                userId, sessionId, metadata.UserId);
            return null;
        }

        // Update last activity time
        metadata.LastActivityTime = DateTime.UtcNow;

        return metadata.Session;
    }

    /// <summary>
    /// Remove and dispose a session
    /// </summary>
    public async Task<bool> RemoveSessionAsync(string userId, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(sessionId))
        {
            return false;
        }

        if (!_sessions.TryRemove(sessionId, out var metadata))
        {
            return false;
        }

        // Verify user owns this session
        if (metadata.UserId != userId)
        {
            _logger.LogWarning("User {UserId} attempted to remove session {SessionId} owned by {OwnerId}", 
                userId, sessionId, metadata.UserId);
            
            // Put it back
            _sessions.TryAdd(sessionId, metadata);
            return false;
        }

        // Remove user mapping
        _userToSession.TryRemove(userId, out _);

        // Dispose the session
        await Task.Run(() => metadata.Session.Dispose());

        _logger.LogInformation("Removed session {SessionId} for user {UserId}", sessionId, userId);
        return true;
    }

    /// <summary>
    /// Get the number of active sessions
    /// </summary>
    public int GetActiveSessionCount()
    {
        return _sessions.Count;
    }

    /// <summary>
    /// Background cleanup of inactive sessions
    /// </summary>
    private void CleanupInactiveSessions(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var timeout = TimeSpan.FromMinutes(_settings.CopilotInteractiveSessionTimeoutMinutes);
            var now = DateTime.UtcNow;
            var sessionsToRemove = new List<(string SessionId, string UserId)>();

            foreach (var kvp in _sessions)
            {
                var timeSinceLastActivity = now - kvp.Value.LastActivityTime;
                if (timeSinceLastActivity >= timeout)
                {
                    sessionsToRemove.Add((kvp.Key, kvp.Value.UserId));
                }
            }

            if (sessionsToRemove.Any())
            {
                _logger.LogInformation("Cleaning up {Count} inactive sessions", sessionsToRemove.Count);

                foreach (var (sessionId, userId) in sessionsToRemove)
                {
                    _ = RemoveSessionAsync(userId, sessionId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during session cleanup");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Stop cleanup timer
        _cleanupTimer?.Dispose();

        // Dispose all sessions
        foreach (var kvp in _sessions)
        {
            try
            {
                kvp.Value.Session.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing session {SessionId}", kvp.Key);
            }
        }

        _sessions.Clear();
        _userToSession.Clear();

        _logger.LogInformation("CopilotSessionService disposed");
    }
}
