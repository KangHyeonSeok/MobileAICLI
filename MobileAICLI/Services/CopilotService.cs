using System.Diagnostics;
using Microsoft.Extensions.Options;
using MobileAICLI.Models;

namespace MobileAICLI.Services;

public class CopilotService
{
    private readonly MobileAICLISettings _settings;
    private readonly ILogger<CopilotService> _logger;

    public CopilotService(IOptions<MobileAICLISettings> settings, ILogger<CopilotService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<(bool Success, string Output, string Error)> AskCopilotAsync(string prompt)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return (false, string.Empty, "Please provide a prompt");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{_settings.GitHubCopilotCommand} suggest \\\"{prompt.Replace("\"", "\\\\\\\"")}\\\"\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = _settings.RepositoryPath
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0 && string.IsNullOrEmpty(output))
            {
                return (false, string.Empty, error.Contains("gh: command not found") 
                    ? "GitHub CLI (gh) is not installed. Please install it to use Copilot features."
                    : error);
            }

            return (true, output, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Copilot command with prompt: {Prompt}", prompt);
            return (false, string.Empty, $"Error: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Output, string Error)> ExplainCommandAsync(string command)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return (false, string.Empty, "Please provide a command to explain");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{_settings.GitHubCopilotCommand} explain \\\"{command.Replace("\"", "\\\\\\\"")}\\\"\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = _settings.RepositoryPath
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0 && string.IsNullOrEmpty(output))
            {
                return (false, string.Empty, error.Contains("gh: command not found")
                    ? "GitHub CLI (gh) is not installed. Please install it to use Copilot features."
                    : error);
            }

            return (true, output, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Copilot explain for command: {Command}", command);
            return (false, string.Empty, $"Error: {ex.Message}");
        }
    }
}
