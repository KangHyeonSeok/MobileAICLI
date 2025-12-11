namespace MobileAICLI.Models;

public class MobileAICLISettings
{
    public string RepositoryPath { get; set; } = "/home/user/repos";
    public string GitHubCopilotCommand { get; set; } = "gh copilot";
    public string GitHubCliPath { get; set; } = "gh";
    public string GitCliPath { get; set; } = "git";
    public bool EnableCopilotMock { get; set; } = false;
    public List<string> AllowedShellCommands { get; set; } = new();
    public List<string> AllowedRepositoryRoots { get; set; } = new();
    public List<string> AllowedWorkRoots { get; set; } = new();
    public string PasswordHash { get; set; } = "";
    
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

    // Interactive Mode settings
    /// <summary>
    /// Interactive session timeout in minutes (default: 15 minutes)
    /// </summary>
    public int CopilotInteractiveSessionTimeoutMinutes { get; set; } = 15;

    /// <summary>
    /// Maximum number of concurrent interactive sessions across all users (default: 20)
    /// </summary>
    public int CopilotInteractiveMaxSessions { get; set; } = 20;

    /// <summary>
    /// Timeout in seconds to wait for prompt pattern after sending a message (default: 3 seconds)
    /// </summary>
    public int CopilotInteractivePromptTimeoutSeconds { get; set; } = 3;

    /// <summary>
    /// Regular expression pattern to detect copilot prompt (default: "> " at end of line)
    /// </summary>
    public string CopilotInteractivePromptPattern { get; set; } = @">\s?$";

    /// <summary>
    /// Maximum length of a single prompt in characters (default: 10000)
    /// </summary>
    public int CopilotInteractiveMaxPromptLength { get; set; } = 10000;

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
