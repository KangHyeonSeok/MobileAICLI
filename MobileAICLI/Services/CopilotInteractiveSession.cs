using Microsoft.Extensions.Options;
using MobileAICLI.Models;
using Pty.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace MobileAICLI.Services;

/// <summary>
/// Manages a single interactive Copilot session with PTY process.
/// Handles stdin/stdout communication, response boundary detection, and ANSI filtering.
/// </summary>
public class CopilotInteractiveSession : ICopilotInteractiveSession
{
    private readonly MobileAICLISettings _settings;
    private readonly ILogger<CopilotInteractiveSession> _logger;
    private IPtyConnection? _ptyConnection;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly StringBuilder _outputBuffer = new();
    private readonly SemaphoreSlim _readLock = new(1, 1);
    private TaskCompletionSource<bool>? _initTcs;
    private bool _disposed;

    // ANSI escape code regex patterns
    private static readonly Regex AnsiEscapeCodePattern = new(@"\x1b\[[0-9;]*[A-Za-z]|\x1b\][^\x07]*\x07", RegexOptions.Compiled);
    
    public string SessionId { get; }
    public bool IsReady { get; private set; }

    public CopilotInteractiveSession(
        string sessionId,
        IOptions<MobileAICLISettings> settings,
        ILogger<CopilotInteractiveSession> logger)
    {
        SessionId = sessionId;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Initialize the PTY process and wait for the initial prompt
    /// </summary>
    public async Task<(bool Success, string Error)> InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return (false, "Session has been disposed");
        }

        try
        {
            _logger.LogInformation("Initializing interactive copilot session {SessionId}", SessionId);

            // Set up initialization completion source
            _initTcs = new TaskCompletionSource<bool>();

            // Start PTY process with Pty.Net
            // Note: Pty.Net 0.1.16-pre uses: Spawn(command, width, height, workingDir, options)
            // BackendOptions can be null or new instance
            _ptyConnection = PtyProvider.Spawn(
                command: "copilot",
                width: _settings.PtyWidth,
                height: _settings.PtyHeight,
                workingDirectory: _settings.RepositoryPath,
                options: new BackendOptions()
            );

            // Subscribe to data events
            _ptyConnection.PtyData += OnDataReceived;
            _ptyConnection.PtyDisconnected += OnProcessExited;

            // Wait for initial prompt with timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            try
            {
                // Wait for initialization to complete (signaled by OnDataReceived when prompt detected)
                await _initTcs.Task.WaitAsync(cts.Token);
                IsReady = true;
                _logger.LogInformation("Session {SessionId} initialized successfully", SessionId);
                return (true, string.Empty);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Initial prompt timeout for session {SessionId}, but considering ready if we got output", SessionId);
                // If we got any output, consider it ready
                lock (_outputBuffer)
                {
                    IsReady = _outputBuffer.Length > 0;
                }
                if (IsReady)
                {
                    return (true, string.Empty);
                }
                return (false, "Failed to initialize: prompt timeout");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize copilot session {SessionId}", SessionId);
            return (false, $"Failed to initialize: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle data received from PTY process
    /// </summary>
    private void OnDataReceived(object? sender, string data)
    {
        lock (_outputBuffer)
        {
            _outputBuffer.Append(data);
            
            // Check for initial prompt during initialization
            if (_initTcs != null && !_initTcs.Task.IsCompleted)
            {
                var promptPattern = new Regex(_settings.CopilotInteractivePromptPattern, RegexOptions.Multiline);
                if (promptPattern.IsMatch(_outputBuffer.ToString()))
                {
                    _logger.LogDebug("Initial prompt detected for session {SessionId}", SessionId);
                    _initTcs.TrySetResult(true);
                }
            }
        }
    }

    /// <summary>
    /// Handle process exit
    /// </summary>
    private void OnProcessExited(object? sender)
    {
        _logger.LogWarning("Copilot process exited for session {SessionId}", SessionId);
        IsReady = false;
        _initTcs?.TrySetException(new InvalidOperationException("Process exited during initialization"));
    }

    /// <summary>
    /// Write a message to the copilot process stdin
    /// </summary>
    public async Task WriteAsync(string text, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CopilotInteractiveSession));
        }

        if (_ptyConnection == null)
        {
            throw new InvalidOperationException("Session not initialized");
        }

        if (!IsReady)
        {
            throw new InvalidOperationException("Session not ready");
        }

        // Validate input length
        if (text.Length > _settings.CopilotInteractiveMaxPromptLength)
        {
            throw new ArgumentException($"Prompt length exceeds maximum of {_settings.CopilotInteractiveMaxPromptLength} characters");
        }

        // Filter dangerous control characters
        var sanitized = SanitizeInput(text);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            // Clear output buffer before writing new prompt
            lock (_outputBuffer)
            {
                _outputBuffer.Clear();
            }

            // Write to PTY
            await _ptyConnection.WriteAsync(sanitized + "\n");
            _logger.LogDebug("Wrote message to session {SessionId}: {Length} chars", SessionId, sanitized.Length);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Read response from copilot process stdout with boundary detection
    /// </summary>
    public async IAsyncEnumerable<string> ReadResponseAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CopilotInteractiveSession));
        }

        if (_ptyConnection == null)
        {
            throw new InvalidOperationException("Session not initialized");
        }

        var promptPattern = new Regex(_settings.CopilotInteractivePromptPattern, RegexOptions.Multiline);
        var lastOutputLength = 0;
        var lastOutputTime = DateTime.UtcNow;
        var timeoutSeconds = _settings.CopilotInteractivePromptTimeoutSeconds;
        var noNewDataCount = 0;

        await _readLock.WaitAsync(cancellationToken);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Wait a bit for data to accumulate
                await Task.Delay(_settings.CopilotInteractivePollingIntervalMs, cancellationToken);

                string currentContent;
                lock (_outputBuffer)
                {
                    currentContent = _outputBuffer.ToString();
                }

                // Check if we have new data
                if (currentContent.Length == lastOutputLength)
                {
                    noNewDataCount++;
                    var timeSinceLastOutput = DateTime.UtcNow - lastOutputTime;

                    // If no new data for timeout duration, complete the response
                    if (timeSinceLastOutput.TotalSeconds >= timeoutSeconds && currentContent.Length > 0)
                    {
                        _logger.LogDebug("Response timeout for session {SessionId}", SessionId);
                        var finalContent = FilterAnsiCodes(currentContent);
                        
                        // Remove trailing prompt if present
                        var match = promptPattern.Match(finalContent);
                        if (match.Success)
                        {
                            finalContent = finalContent.Substring(0, match.Index);
                        }
                        
                        if (!string.IsNullOrEmpty(finalContent.Trim()))
                        {
                            yield return finalContent;
                        }
                        yield break;
                    }

                    // No data and no reason to wait, stop
                    if (noNewDataCount > 20 && currentContent.Length == 0)
                    {
                        yield break;
                    }
                }
                else
                {
                    // New data received
                    lastOutputLength = currentContent.Length;
                    lastOutputTime = DateTime.UtcNow;
                    noNewDataCount = 0;

                    // Check for prompt pattern (response complete)
                    var match = promptPattern.Match(currentContent);
                    if (match.Success)
                    {
                        _logger.LogDebug("Prompt pattern detected for session {SessionId}", SessionId);
                        var finalContent = currentContent.Substring(0, match.Index);
                        finalContent = FilterAnsiCodes(finalContent);
                        
                        if (!string.IsNullOrEmpty(finalContent.Trim()))
                        {
                            yield return finalContent;
                        }
                        yield break;
                    }

                    // Yield chunks periodically (every 512 chars)
                    if (currentContent.Length >= 512)
                    {
                        var chunkSize = Math.Min(512, currentContent.Length);
                        var chunk = FilterAnsiCodes(currentContent.Substring(0, chunkSize));
                        
                        if (!string.IsNullOrEmpty(chunk.Trim()))
                        {
                            yield return chunk;
                        }

                        // Remove yielded content from buffer
                        lock (_outputBuffer)
                        {
                            _outputBuffer.Remove(0, chunkSize);
                        }
                        lastOutputLength = 0; // Reset to recount
                    }
                }
            }
        }
        finally
        {
            _readLock.Release();
            _logger.LogDebug("ReadResponseAsync completed for session {SessionId}", SessionId);
        }
    }

    /// <summary>
    /// Sanitize user input to prevent command injection
    /// </summary>
    private string SanitizeInput(string input)
    {
        // Remove control characters that could interfere with PTY
        // Filter: exit, EOF (Ctrl+D), etc.
        var sanitized = input
            .Replace("\x04", "") // EOF
            .Replace("\x03", "") // ETX (Ctrl+C)
            .Replace("\x1A", ""); // SUB (Ctrl+Z)

        // Remove "exit" command attempts (case-insensitive)
        if (sanitized.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
        {
            return ""; // Return empty to prevent session termination
        }

        return sanitized;
    }

    /// <summary>
    /// Filter ANSI escape codes from terminal output
    /// </summary>
    private string FilterAnsiCodes(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        return AnsiEscapeCodePattern.Replace(text, "");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        IsReady = false;

        try
        {
            // Try to gracefully exit
            if (_ptyConnection != null)
            {
                try
                {
                    _ptyConnection.Write("exit\n");
                    Task.Delay(100).Wait(); // Give it a moment to process
                }
                catch
                {
                    // Ignore errors during shutdown
                }

                // Unsubscribe from events
                _ptyConnection.PtyData -= OnDataReceived;
                _ptyConnection.PtyDisconnected -= OnProcessExited;
                
                _ptyConnection.Dispose();
            }
        }
        catch
        {
            // Ignore errors during disposal
        }

        _writeLock?.Dispose();
        _readLock?.Dispose();

        _logger.LogInformation("Disposed interactive copilot session {SessionId}", SessionId);
    }
}
