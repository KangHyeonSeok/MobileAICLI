using Microsoft.AspNetCore.SignalR;
using MobileAICLI.Services;
using static MobileAICLI.Services.FileService;

namespace MobileAICLI.Hubs;

/// <summary>
/// 통합 테스트용 SignalR Hub - 모든 서비스 기능을 노출
/// </summary>
public class TestHub : Hub
{
    private readonly ShellStreamingService _shellService;
    private readonly FileService _fileService;
    private readonly TerminalService _terminalService;
    private readonly CopilotService _copilotService;
    private readonly CopilotStreamingService _copilotStreamingService;
    private readonly ILogger<TestHub> _logger;

    public TestHub(
        ShellStreamingService shellService,
        FileService fileService,
        TerminalService terminalService,
        CopilotService copilotService,
        CopilotStreamingService copilotStreamingService,
        ILogger<TestHub> logger)
    {
        _shellService = shellService;
        _fileService = fileService;
        _terminalService = terminalService;
        _copilotService = copilotService;
        _copilotStreamingService = copilotStreamingService;
        _logger = logger;
    }

    #region Shell (Streaming)

    /// <summary>
    /// 셸 명령 실행 (스트리밍)
    /// Client receives: ReceiveShellOutput, ReceiveShellError, ShellComplete
    /// </summary>
    public async Task ExecuteShell(string command)
    {
        _logger.LogInformation("TestHub.ExecuteShell: {Command}", command);

        int exitCode = 0;
        try
        {
            await foreach (var output in _shellService.ExecuteStreamingAsync(command))
            {
                switch (output.Type)
                {
                    case ShellOutputType.Output:
                        await Clients.Caller.SendAsync("ReceiveShellOutput", output.Content);
                        break;
                    case ShellOutputType.Error:
                        await Clients.Caller.SendAsync("ReceiveShellError", output.Content);
                        break;
                    case ShellOutputType.Complete:
                        exitCode = output.ExitCode ?? 0;
                        break;
                }
            }

            await Clients.Caller.SendAsync("ShellComplete", exitCode, "");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing shell command: {Command}", command);
            await Clients.Caller.SendAsync("ShellComplete", 1, ex.Message);
        }
    }

    #endregion

    #region Files (Request-Response)

    /// <summary>
    /// 파일 목록 조회
    /// </summary>
    public async Task<List<FileItem>> GetFiles(string? path = null)
    {
        _logger.LogInformation("TestHub.GetFiles: {Path}", path ?? "/");
        return await _fileService.GetFilesAsync(path);
    }

    /// <summary>
    /// 파일 읽기
    /// </summary>
    public async Task<FileResult> ReadFile(string path)
    {
        _logger.LogInformation("TestHub.ReadFile: {Path}", path);
        var (success, content) = await _fileService.ReadFileAsync(path);
        return new FileResult(success, content, success ? null : content);
    }

    /// <summary>
    /// 파일 쓰기
    /// </summary>
    public async Task<WriteResult> WriteFile(string path, string content)
    {
        _logger.LogInformation("TestHub.WriteFile: {Path}", path);
        var (success, message) = await _fileService.WriteFileAsync(path, content);
        return new WriteResult(success, message);
    }

    #endregion

    #region Terminal (Request-Response, Whitelisted)

    /// <summary>
    /// 터미널 명령 실행 (화이트리스트 기반)
    /// </summary>
    public async Task<TerminalResult> ExecuteTerminal(string command)
    {
        _logger.LogInformation("TestHub.ExecuteTerminal: {Command}", command);
        var (success, output, error) = await _terminalService.ExecuteCommandAsync(command);
        return new TerminalResult(success, output, error, success ? 0 : 1);
    }

    #endregion

    #region Copilot (Streaming)

    /// <summary>
    /// Copilot에 질문 (스트리밍)
    /// Client receives: ReceiveCopilotOutput, ReceiveCopilotError, CopilotComplete
    /// </summary>
    public async Task AskCopilot(string prompt)
    {
        _logger.LogInformation("TestHub.AskCopilot: {Prompt}", prompt);

        try
        {
            await foreach (var output in _copilotStreamingService.SendPromptStreamingAsync(prompt))
            {
                switch (output.Type)
                {
                    case CopilotOutputType.Output:
                        await Clients.Caller.SendAsync("ReceiveCopilotOutput", output.Content);
                        break;
                    case CopilotOutputType.Error:
                        await Clients.Caller.SendAsync("ReceiveCopilotError", output.Content);
                        break;
                    case CopilotOutputType.Complete:
                        await Clients.Caller.SendAsync("CopilotComplete", output.Success ?? false, output.ErrorMessage ?? "");
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error asking Copilot: {Prompt}", prompt);
            await Clients.Caller.SendAsync("CopilotComplete", false, ex.Message);
        }
    }

    /// <summary>
    /// Copilot 상태 확인 (설치 및 인증)
    /// </summary>
    public async Task<CopilotStatusInfo> CheckCopilotStatus()
    {
        _logger.LogInformation("TestHub.CheckCopilotStatus");

        var (installed, version, installError) = await _copilotStreamingService.CheckInstallationAsync();
        var (authenticated, user, authError) = await _copilotStreamingService.CheckAuthStatusAsync();

        return new CopilotStatusInfo(
            installed,
            version,
            authenticated,
            user,
            installed ? (authenticated ? null : authError) : installError
        );
    }

    /// <summary>
    /// 명령어 설명 요청
    /// </summary>
    public async Task ExplainCommand(string command)
    {
        _logger.LogInformation("TestHub.ExplainCommand: {Command}", command);

        try
        {
            var (success, output, error) = await _copilotService.ExplainCommandAsync(command);

            if (success)
            {
                await Clients.Caller.SendAsync("ReceiveCopilotOutput", output);
                await Clients.Caller.SendAsync("CopilotComplete", true, "");
            }
            else
            {
                await Clients.Caller.SendAsync("CopilotComplete", false, error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error explaining command: {Command}", command);
            await Clients.Caller.SendAsync("CopilotComplete", false, ex.Message);
        }
    }

    #endregion

    #region Connection Events

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("TestHub client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("TestHub client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    #endregion

    #region Result Types

    public record FileResult(bool Success, string Content, string? Error);
    public record WriteResult(bool Success, string Message);
    public record TerminalResult(bool Success, string Output, string Error, int ExitCode);
    public record CopilotStatusInfo(bool Installed, string? Version, bool Authenticated, string? User, string? Error);

    #endregion
}
