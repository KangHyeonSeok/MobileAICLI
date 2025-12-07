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
}
