namespace MobileAICLI.Models;

public class MobileAICLISettings
{
    public string RepositoryPath { get; set; } = "/home/user/repos";
    public string GitHubCopilotCommand { get; set; } = "gh copilot";
    public List<string> AllowedShellCommands { get; set; } = new();
}
