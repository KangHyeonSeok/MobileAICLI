namespace MobileAICLI.Services;

/// <summary>
/// Represents a single interactive Copilot session with PTY process management.
/// Each session maintains a persistent copilot process for context-aware conversations.
/// Phase 2: Interactive Mode - Issue 0
/// </summary>
public interface ICopilotInteractiveSession : IDisposable
{
    /// <summary>
    /// Unique identifier for this session
    /// </summary>
    string SessionId { get; }

    /// <summary>
    /// Indicates if the session is ready to accept input
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Initialize the interactive copilot process and wait for the initial prompt
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    /// Task representing the initialization operation.
    /// Returns a tuple (Success, Error) where Success indicates if initialization succeeded,
    /// and Error contains an error message if initialization failed.
    /// </returns>
    Task<(bool Success, string Error)> InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Write a message to the copilot process stdin
    /// </summary>
    /// <param name="text">Text to send to the copilot process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the write operation</returns>
    Task WriteAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Read the response from the copilot process stdout asynchronously.
    /// Returns chunks of output until the prompt pattern is detected or timeout occurs.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of response chunks</returns>
    IAsyncEnumerable<string> ReadResponseAsync(CancellationToken cancellationToken = default);
}
