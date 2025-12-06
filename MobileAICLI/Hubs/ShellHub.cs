using Microsoft.AspNetCore.SignalR;
using MobileAICLI.Services;

namespace MobileAICLI.Hubs;

/// <summary>
/// 쉘 명령 실행을 위한 SignalR Hub
/// Phase 1.1.1: 기본 구조 검증용
/// </summary>
public class ShellHub : Hub
{
    private readonly ShellStreamingService _shellService;
    private readonly ILogger<ShellHub> _logger;

    public ShellHub(ShellStreamingService shellService, ILogger<ShellHub> logger)
    {
        _shellService = shellService;
        _logger = logger;
    }

    /// <summary>
    /// 클라이언트에서 명령 실행 요청
    /// </summary>
    public async Task ExecuteCommand(string command)
    {
        _logger.LogInformation("ExecuteCommand called with: {Command}", command);

        try
        {
            await foreach (var output in _shellService.ExecuteStreamingAsync(command, Context.ConnectionAborted))
            {
                switch (output.Type)
                {
                    case ShellOutputType.Output:
                        await Clients.Caller.SendAsync("ReceiveOutput", output.Content, Context.ConnectionAborted);
                        break;
                    
                    case ShellOutputType.Error:
                        await Clients.Caller.SendAsync("ReceiveError", output.Content, Context.ConnectionAborted);
                        break;
                    
                    case ShellOutputType.Complete:
                        await Clients.Caller.SendAsync("ReceiveComplete", output.ExitCode, Context.ConnectionAborted);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ExecuteCommand");
            await Clients.Caller.SendAsync("ReceiveError", $"Hub error: {ex.Message}", Context.ConnectionAborted);
            await Clients.Caller.SendAsync("ReceiveComplete", -1, Context.ConnectionAborted);
        }
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}, Exception: {Exception}", 
            Context.ConnectionId, exception?.Message);
        await base.OnDisconnectedAsync(exception);
    }
}
