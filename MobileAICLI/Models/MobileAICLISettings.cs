namespace MobileAICLI.Models;

public class MobileAICLISettings
{
    public string RepositoryPath { get; set; } = "/home/user/repos";
    public string GitHubCopilotCommand { get; set; } = "gh copilot";
    public string GitHubCliPath { get; set; } = "gh";
    public bool EnableCopilotMock { get; set; } = false;
    public List<string> AllowedShellCommands { get; set; } = new();
    
    // Authentication settings
    public bool EnableAuthentication { get; set; } = true;
    public int SessionTimeoutMinutes { get; set; } = 30;
    public int MaxFailedLoginAttempts { get; set; } = 5;
    public int FailedLoginDelaySeconds { get; set; } = 1;
    public int RateLimitResetMinutes { get; set; } = 15;
    /// <summary>
    /// Default Copilot model to use
    /// </summary>
    public string CopilotModel { get; set; } = "default";
    
    /// <summary>
    /// List of allowed Copilot models for security validation
    /// </summary>
    public List<string> AllowedCopilotModels { get; set; } = new() { "default", "gpt-4", "gpt-3.5-turbo", "claude-3.5-sonnet" };

    /// <summary>
    /// Validates model name and returns default value if model is not allowed
    /// </summary>
    public string ValidateModel(string? model)
    {
        // Use default model if no model is specified or empty
        if (string.IsNullOrWhiteSpace(model))
        {
            return CopilotModel;
        }

        // Check if model is in allowed list
        if (AllowedCopilotModels.Contains(model, StringComparer.OrdinalIgnoreCase))
        {
            return model;
        }

        // Use default model if model is not allowed (caller should log warning)
        return CopilotModel;
    }
}
