using System.Diagnostics;
using Microsoft.Extensions.Options;
using MobileAICLI.Models;

namespace MobileAICLI.Services;

public class TerminalService
{
    private readonly MobileAICLISettings _settings;
    private readonly ILogger<TerminalService> _logger;

    public TerminalService(IOptions<MobileAICLISettings> settings, ILogger<TerminalService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<(bool Success, string Output, string Error)> ExecuteCommandAsync(string command)
    {
        try
        {
            // Check if command is allowed and safe
            if (!IsCommandAllowed(command))
            {
                return (false, string.Empty, "Command not allowed. Check your whitelist configuration.");
            }

            if (ContainsDangerousCharacters(command))
            {
                return (false, string.Empty, "Command contains dangerous characters or operators (;, |, &, &&, ||, >, <, `, $, newlines)");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = _settings.RepositoryPath
            };

            // Use ArgumentList for safer command execution
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(command);

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            var output = await outputTask;
            var error = await errorTask;

            return (process.ExitCode == 0, output, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command: {Command}", command);
            return (false, string.Empty, $"Error: {ex.Message}");
        }
    }

    private bool ContainsDangerousCharacters(string command)
    {
        // Prevent command chaining and shell injection
        char[] dangerousChars = { ';', '|', '&', '>', '<', '`', '$', '\n', '\r' };
        return command.IndexOfAny(dangerousChars) >= 0 || command.Contains("&&") || command.Contains("||");
    }

    private bool IsCommandAllowed(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return false;

        var commandParts = command.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (commandParts.Length == 0)
            return false;

        // Check if the base command or the full command prefix is in the allowed list
        foreach (var allowedCommand in _settings.AllowedShellCommands)
        {
            var allowedParts = allowedCommand.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            // Check if the command starts with the allowed command
            if (commandParts.Length >= allowedParts.Length)
            {
                bool matches = true;
                for (int i = 0; i < allowedParts.Length; i++)
                {
                    if (commandParts[i] != allowedParts[i])
                    {
                        matches = false;
                        break;
                    }
                }
                if (matches)
                    return true;
            }
        }

        return false;
    }
}
