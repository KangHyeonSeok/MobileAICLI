using System.Diagnostics;
using Microsoft.Extensions.Options;
using MobileAICLI.Models;

namespace MobileAICLI.Services;

public class CopilotService
{
    private readonly MobileAICLISettings _settings;
    private readonly RepositoryContext _context;
    private readonly ILogger<CopilotService> _logger;

    public CopilotService(IOptions<MobileAICLISettings> settings, RepositoryContext context, ILogger<CopilotService> logger)
    {
        _settings = settings.Value;
        _context = context;
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

            if (string.IsNullOrWhiteSpace(_settings.GitHubCopilotCommand))
            {
                return (false, string.Empty, "GitHub Copilot command is not configured");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = _settings.GitHubCopilotCommand.Split(' ')[0],
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = _context.GetAbsolutePath()
            };

            // Use ArgumentList for safer command execution
            var commandParts = _settings.GitHubCopilotCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < commandParts.Length; i++)
            {
                startInfo.ArgumentList.Add(commandParts[i]);
            }
            startInfo.ArgumentList.Add("suggest");
            startInfo.ArgumentList.Add(prompt);

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0 && string.IsNullOrEmpty(output))
            {
                return (false, string.Empty, error.Contains("gh: command not found") || error.Contains("not found")
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

            if (string.IsNullOrWhiteSpace(_settings.GitHubCopilotCommand))
            {
                return (false, string.Empty, "GitHub Copilot command is not configured");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = _settings.GitHubCopilotCommand.Split(' ')[0],
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = _context.GetAbsolutePath()
            };

            // Use ArgumentList for safer command execution
            var commandParts = _settings.GitHubCopilotCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < commandParts.Length; i++)
            {
                startInfo.ArgumentList.Add(commandParts[i]);
            }
            startInfo.ArgumentList.Add("explain");
            startInfo.ArgumentList.Add(command);

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0 && string.IsNullOrEmpty(output))
            {
                return (false, string.Empty, error.Contains("gh: command not found") || error.Contains("not found")
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
