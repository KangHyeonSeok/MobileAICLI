using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using MobileAICLI.Models;

namespace MobileAICLI.Services;

public class ToolDiscoveryService
{
    private readonly ILogger<ToolDiscoveryService> _logger;

    public ToolDiscoveryService(ILogger<ToolDiscoveryService> logger)
    {
        _logger = logger;
    }

    public async Task<ToolProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var result = new ToolProbeResult();

        try
        {
            var command = GetProbeCommand();
            if (command is null)
            {
                result.Error = "Unsupported OS for tool probing.";
                return result;
            }

            var copilotProbe = await RunProbeAsync(command.Value, "copilot", cancellationToken);
            result.CopilotPaths = copilotProbe.Paths;
            if (!string.IsNullOrEmpty(copilotProbe.Diagnostic))
            {
                result.Diagnostics["copilot"] = copilotProbe.Diagnostic;
            }

            if (result.CopilotPaths.Count == 0)
            {
                var npmPaths = await ProbeCopilotViaNpmAsync(cancellationToken);
                foreach (var p in npmPaths)
                {
                    if (!result.CopilotPaths.Contains(p, StringComparer.OrdinalIgnoreCase))
                    {
                        result.CopilotPaths.Add(p);
                    }
                }

                if (result.CopilotPaths.Count == 0)
                {
                    result.Diagnostics["copilot"] = "Copilot not found via which/where and npm global bin. Ensure npm global bin is on PATH or install Copilot CLI.";
                }
            }

            var ghProbe = await RunProbeAsync(command.Value, "gh", cancellationToken);
            result.GhPaths = ghProbe.Paths;
            if (!string.IsNullOrEmpty(ghProbe.Diagnostic))
            {
                result.Diagnostics["gh"] = ghProbe.Diagnostic;
            }

            var gitProbe = await RunProbeAsync(command.Value, "git", cancellationToken);
            result.GitPaths = gitProbe.Paths;
            if (!string.IsNullOrEmpty(gitProbe.Diagnostic))
            {
                result.Diagnostics["git"] = gitProbe.Diagnostic;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error probing tools");
            result.Error = ex.Message;
        }

        return result;
    }

    private (string FileName, string ArgumentPrefix) ? GetProbeCommand()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ("where", string.Empty);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return ("which", string.Empty);
        }

        return null;
    }

    private async Task<(List<string> Paths, string? Diagnostic)> RunProbeAsync((string FileName, string ArgumentPrefix) command, string toolName, CancellationToken cancellationToken)
    {
        var paths = new List<string>();
        string? diagnostic = null;

        var psi = new ProcessStartInfo
        {
            FileName = command.FileName,
            Arguments = string.IsNullOrEmpty(command.ArgumentPrefix) ? toolName : $"{command.ArgumentPrefix} {toolName}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi }; 
        process.Start();

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(stdout))
        {
            var lines = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed) && !paths.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                {
                    if (File.Exists(trimmed))
                    {
                        paths.Add(trimmed);
                    }
                }
            }
        }
        else
        {
            diagnostic = $"Probe '{command.FileName} {toolName}' exited {process.ExitCode}. Tool may not be in PATH for the server.";
            _logger.LogInformation("Probe for {Tool} returned exit {Code}: {Error}", toolName, process.ExitCode, stderr.Trim());
        }

        if (paths.Count == 0 && diagnostic is null)
        {
            diagnostic = $"Probe '{command.FileName} {toolName}' returned no results.";
        }

        return (paths, diagnostic);
    }

    private async Task<List<string>> ProbeCopilotViaNpmAsync(CancellationToken cancellationToken)
    {
        var paths = new List<string>();
        var npmExe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "npm.cmd" : "npm";

        try
        {
            var npmBin = await RunSimpleCommandAsync(npmExe, "bin -g", cancellationToken);
            if (string.IsNullOrWhiteSpace(npmBin))
            {
                return paths;
            }

            var binDir = npmBin.Trim();
            if (!Directory.Exists(binDir))
            {
                return paths;
            }

            var candidates = new[]
            {
                Path.Combine(binDir, "copilot"),
                Path.Combine(binDir, "copilot.cmd"),
                Path.Combine(binDir, "copilot.exe")
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate) && !paths.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                {
                    paths.Add(candidate);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "npm-based copilot detection failed");
        }

        return paths;
    }

    private async Task<string?> RunSimpleCommandAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            _logger.LogInformation("Command {Command} exited with {Code}: {Error}", fileName, process.ExitCode, stderr.Trim());
            return null;
        }

        return stdout;
    }
}
