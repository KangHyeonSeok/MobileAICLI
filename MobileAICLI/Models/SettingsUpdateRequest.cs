namespace MobileAICLI.Models;

public class SettingsUpdateRequest
{
    public string? RepositoryPath { get; set; }
    public string? GitHubCopilotCommand { get; set; }
    public string? GitHubCliPath { get; set; }
    public string? GitCliPath { get; set; }
    public List<string>? AllowedShellCommands { get; set; }
    public List<string>? AllowedWorkRoots { get; set; }
}

public class PasswordChangeRequest
{
    public string CurrentPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
}

public class SettingsUpdateResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public List<string> ValidationErrors { get; set; } = new();
}
