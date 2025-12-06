using Microsoft.AspNetCore.SignalR.Client;

namespace MobileAICLI.TestClient.Services;

/// <summary>
/// SignalR Hub 연결 관리 서비스
/// </summary>
public class HubConnectionService
{
    private readonly string _serverUrl;
    private HubConnection? _connection;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public HubConnectionService(string serverUrl)
    {
        _serverUrl = serverUrl.TrimEnd('/');
    }

    public async Task<bool> ConnectAsync()
    {
        try
        {
            _connection = new HubConnectionBuilder()
                .WithUrl($"{_serverUrl}/testhub")
                .WithAutomaticReconnect()
                .Build();

            _connection.Closed += (error) =>
            {
                Console.Error.WriteLine($"Connection closed: {error?.Message}");
                return Task.CompletedTask;
            };

            await _connection.StartAsync();
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Connection failed: {ex.Message}");
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_connection != null)
        {
            await _connection.StopAsync();
            await _connection.DisposeAsync();
        }
    }

    #region Shell (Streaming)

    public async Task<CommandResult> ExecuteShellAsync(string command, Action<string>? onOutput = null, Action<string>? onError = null)
    {
        if (_connection == null) return CommandResult.Failure("Not connected");

        var result = new CommandResult();
        var stdout = new List<string>();
        var stderr = new List<string>();
        var tcs = new TaskCompletionSource<bool>();

        IDisposable? outputHandler = null;
        IDisposable? errorHandler = null;
        IDisposable? completeHandler = null;

        try
        {
            outputHandler = _connection.On<string>("ReceiveShellOutput", (text) =>
            {
                stdout.Add(text);
                onOutput?.Invoke(text);
            });

            errorHandler = _connection.On<string>("ReceiveShellError", (text) =>
            {
                stderr.Add(text);
                onError?.Invoke(text);
            });

            completeHandler = _connection.On<int, string>("ShellComplete", (exitCode, error) =>
            {
                result.ExitCode = exitCode;
                result.Stdout = string.Join("", stdout);
                result.Stderr = string.Join("", stderr);
                result.Success = exitCode == 0;
                if (!string.IsNullOrEmpty(error))
                {
                    result.Error = error;
                }
                tcs.TrySetResult(true);
            });

            await _connection.InvokeAsync("ExecuteShell", command);

            // 타임아웃 설정 (60초)
            var timeoutTask = Task.Delay(60000);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                result.Success = false;
                result.Error = "Command timed out";
                result.ExitCode = -1;
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            result.ExitCode = -1;
        }
        finally
        {
            outputHandler?.Dispose();
            errorHandler?.Dispose();
            completeHandler?.Dispose();
        }

        return result;
    }

    #endregion

    #region Files (Request-Response)

    public async Task<List<FileItem>> GetFilesAsync(string? path = null)
    {
        if (_connection == null) return new List<FileItem>();

        try
        {
            return await _connection.InvokeAsync<List<FileItem>>("GetFiles", path);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"GetFiles error: {ex.Message}");
            return new List<FileItem>();
        }
    }

    public async Task<FileResult> ReadFileAsync(string path)
    {
        if (_connection == null) return new FileResult(false, "", "Not connected");

        try
        {
            return await _connection.InvokeAsync<FileResult>("ReadFile", path);
        }
        catch (Exception ex)
        {
            return new FileResult(false, "", ex.Message);
        }
    }

    public async Task<WriteResult> WriteFileAsync(string path, string content)
    {
        if (_connection == null) return new WriteResult(false, "Not connected");

        try
        {
            return await _connection.InvokeAsync<WriteResult>("WriteFile", path, content);
        }
        catch (Exception ex)
        {
            return new WriteResult(false, ex.Message);
        }
    }

    #endregion

    #region Terminal (Request-Response)

    public async Task<TerminalResult> ExecuteTerminalAsync(string command)
    {
        if (_connection == null) return new TerminalResult(false, "", "Not connected", -1);

        try
        {
            return await _connection.InvokeAsync<TerminalResult>("ExecuteTerminal", command);
        }
        catch (Exception ex)
        {
            return new TerminalResult(false, "", ex.Message, -1);
        }
    }

    #endregion

    #region Copilot (Streaming)

    public async Task<CommandResult> AskCopilotAsync(string prompt, Action<string>? onOutput = null, Action<string>? onError = null)
    {
        if (_connection == null) return CommandResult.Failure("Not connected");

        var result = new CommandResult();
        var output = new List<string>();
        var errors = new List<string>();
        var tcs = new TaskCompletionSource<bool>();

        IDisposable? outputHandler = null;
        IDisposable? errorHandler = null;
        IDisposable? completeHandler = null;

        try
        {
            outputHandler = _connection.On<string>("ReceiveCopilotOutput", (text) =>
            {
                output.Add(text);
                onOutput?.Invoke(text);
            });

            errorHandler = _connection.On<string>("ReceiveCopilotError", (text) =>
            {
                errors.Add(text);
                onError?.Invoke(text);
            });

            completeHandler = _connection.On<bool, string>("CopilotComplete", (success, error) =>
            {
                result.Success = success;
                result.Stdout = string.Join("", output);
                result.Stderr = string.Join("", errors);
                result.Error = error;
                result.ExitCode = success ? 0 : 1;
                tcs.TrySetResult(true);
            });

            await _connection.InvokeAsync("AskCopilot", prompt);

            var timeoutTask = Task.Delay(120000); // 2분 타임아웃
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                result.Success = false;
                result.Error = "Copilot request timed out";
                result.ExitCode = -1;
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            result.ExitCode = -1;
        }
        finally
        {
            outputHandler?.Dispose();
            errorHandler?.Dispose();
            completeHandler?.Dispose();
        }

        return result;
    }

    public async Task<CommandResult> ExplainCommandAsync(string command, Action<string>? onOutput = null)
    {
        if (_connection == null) return CommandResult.Failure("Not connected");

        var result = new CommandResult();
        var output = new List<string>();
        var tcs = new TaskCompletionSource<bool>();

        IDisposable? outputHandler = null;
        IDisposable? completeHandler = null;

        try
        {
            outputHandler = _connection.On<string>("ReceiveCopilotOutput", (text) =>
            {
                output.Add(text);
                onOutput?.Invoke(text);
            });

            completeHandler = _connection.On<bool, string>("CopilotComplete", (success, error) =>
            {
                result.Success = success;
                result.Stdout = string.Join("", output);
                result.Error = error;
                result.ExitCode = success ? 0 : 1;
                tcs.TrySetResult(true);
            });

            await _connection.InvokeAsync("ExplainCommand", command);

            var timeoutTask = Task.Delay(120000);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                result.Success = false;
                result.Error = "Explain request timed out";
                result.ExitCode = -1;
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            result.ExitCode = -1;
        }
        finally
        {
            outputHandler?.Dispose();
            completeHandler?.Dispose();
        }

        return result;
    }

    #endregion
}

#region Result Types

public class CommandResult
{
    public bool Success { get; set; }
    public string Stdout { get; set; } = "";
    public string Stderr { get; set; } = "";
    public string? Error { get; set; }
    public int ExitCode { get; set; }

    public static CommandResult Failure(string error) => new()
    {
        Success = false,
        Error = error,
        ExitCode = -1
    };
}

public record FileItem(string Name, string FullPath, bool IsDirectory, long Size, DateTime LastModified);
public record FileResult(bool Success, string Content, string? Error);
public record WriteResult(bool Success, string Message);
public record TerminalResult(bool Success, string Output, string Error, int ExitCode);

#endregion
