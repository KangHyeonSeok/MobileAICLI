namespace MobileAICLI.Models;

/// <summary>
/// Represents the current status of an interactive Copilot session.
/// Phase 2: Interactive Mode - Issue 0
/// </summary>
public enum CopilotSessionStatus
{
    /// <summary>
    /// Session is being initialized (process starting, waiting for initial prompt)
    /// </summary>
    Initializing,

    /// <summary>
    /// Session is ready to accept input
    /// </summary>
    Ready,

    /// <summary>
    /// Session is currently processing a request
    /// </summary>
    Processing,

    /// <summary>
    /// Session encountered an error
    /// </summary>
    Error,

    /// <summary>
    /// Session has been closed and disposed
    /// </summary>
    Closed
}
