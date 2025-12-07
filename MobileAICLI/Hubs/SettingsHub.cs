using Microsoft.AspNetCore.SignalR;
using MobileAICLI.Models;
using MobileAICLI.Services;

namespace MobileAICLI.Hubs;

public class SettingsHub : Hub
{
    private readonly SettingsService _settingsService;
    private readonly ILogger<SettingsHub> _logger;

    public SettingsHub(SettingsService settingsService, ILogger<SettingsHub> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<MobileAICLISettings> GetSettings()
    {
        try
        {
            _logger.LogInformation("Getting current settings");
            return _settingsService.GetCurrentSettings();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting settings");
            throw;
        }
    }

    public async Task<SettingsUpdateResult> UpdateSettings(SettingsUpdateRequest request)
    {
        try
        {
            _logger.LogInformation("Updating settings");
            return await _settingsService.UpdateSettingsAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating settings");
            return new SettingsUpdateResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public async Task<SettingsUpdateResult> ChangePassword(PasswordChangeRequest request)
    {
        try
        {
            _logger.LogInformation("Changing password");
            return await _settingsService.ChangePasswordAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password");
            return new SettingsUpdateResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }
}
