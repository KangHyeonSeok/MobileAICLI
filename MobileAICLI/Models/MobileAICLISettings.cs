namespace MobileAICLI.Models;

public class MobileAICLISettings
{
    public string RepositoryPath { get; set; } = "/home/user/repos";
    public string GitHubCopilotCommand { get; set; } = "gh copilot";
    public string GitHubCliPath { get; set; } = "gh";
    public bool EnableCopilotMock { get; set; } = false;
    public List<string> AllowedShellCommands { get; set; } = new();
    
    /// <summary>
    /// Default Copilot model to use
    /// </summary>
    public string CopilotModel { get; set; } = "default";
    
    /// <summary>
    /// List of allowed Copilot models for security validation
    /// </summary>
    public List<string> AllowedCopilotModels { get; set; } = new() { "default", "gpt-4", "gpt-3.5-turbo", "claude-3.5-sonnet" };
}
