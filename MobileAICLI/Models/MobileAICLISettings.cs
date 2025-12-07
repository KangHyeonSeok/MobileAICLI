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
}
