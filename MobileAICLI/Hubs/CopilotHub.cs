using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using MobileAICLI.Models;
using MobileAICLI.Services;

namespace MobileAICLI.Hubs;

/// <summary>
/// Copilot CLI 실행을 위한 SignalR Hub
/// Phase 1.1.2: Copilot CLI 통합
/// </summary>
public class CopilotHub : Hub
{
    private readonly CopilotStreamingService _copilotService;
    private readonly MobileAICLISettings _settings;
    private readonly ILogger<CopilotHub> _logger;

    public CopilotHub(CopilotStreamingService copilotService, IOptions<MobileAICLISettings> settings, ILogger<CopilotHub> logger)
    {
        _copilotService = copilotService;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Copilot에 프롬프트 전송
    /// </summary>
    public async Task SendPrompt(string prompt, CopilotToolSettings? toolSettings = null, string? model = null)
    {
        _logger.LogInformation("SendPrompt called with: {Prompt}, Model: {Model}", TruncateForLog(prompt), model ?? "default");

        try
        {
            await foreach (var output in _copilotService.SendPromptStreamingAsync(
                prompt, 
                toolSettings,
                model,
                Context.ConnectionAborted))
            {
                switch (output.Type)
                {
                    case CopilotOutputType.Output:
                        await Clients.Caller.SendAsync("ReceiveOutput", output.Content, Context.ConnectionAborted);
                        break;

                    case CopilotOutputType.Error:
                        await Clients.Caller.SendAsync("ReceiveError", output.Content, Context.ConnectionAborted);
                        break;

                    case CopilotOutputType.Complete:
                        await Clients.Caller.SendAsync("ReceiveComplete", output.Success ?? false, output.ErrorMessage, Context.ConnectionAborted);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SendPrompt");
            await Clients.Caller.SendAsync("ReceiveError", $"Hub error: {ex.Message}", Context.ConnectionAborted);
            await Clients.Caller.SendAsync("ReceiveComplete", false, ex.Message, Context.ConnectionAborted);
        }
    }

    /// <summary>
    /// Copilot CLI 설치 상태 확인
    /// </summary>
    public async Task<CopilotStatusResult> CheckStatus()
    {
        _logger.LogInformation("CheckStatus called");

        try
        {
            var (installed, version, installError) = await _copilotService.CheckInstallationAsync();
            var (authenticated, user, authError) = await _copilotService.CheckAuthStatusAsync();

            return new CopilotStatusResult
            {
                Installed = installed,
                Version = version,
                Authenticated = authenticated,
                User = user,
                Error = installed ? (authenticated ? null : authError) : installError,
                CurrentModel = _settings.CopilotModel,
                AllowedModels = _settings.AllowedCopilotModels
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CheckStatus");
            return new CopilotStatusResult
            {
                Installed = false,
                Authenticated = false,
                Error = ex.Message,
                CurrentModel = _settings.CopilotModel,
                AllowedModels = _settings.AllowedCopilotModels
            };
        }
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Copilot client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Copilot client disconnected: {ConnectionId}, Exception: {Exception}",
            Context.ConnectionId, exception?.Message);
        await base.OnDisconnectedAsync(exception);
    }

    private static string TruncateForLog(string text, int maxLength = 100)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        return text[..maxLength] + "...";
    }
}

/// <summary>
/// Copilot 상태 확인 결과
/// </summary>
public class CopilotStatusResult
{
    public bool Installed { get; set; }
    public string? Version { get; set; }
    public bool Authenticated { get; set; }
    public string? User { get; set; }
    public string? Error { get; set; }
    public string CurrentModel { get; set; } = "default";
    public List<string> AllowedModels { get; set; } = new();
}
