using MobileAICLI.TestClient.Services;

namespace MobileAICLI.TestClient;

/// <summary>
/// 명령어 실행기 - Interactive 모드에서 사용
/// </summary>
public class CommandExecutor
{
    private readonly HubConnectionService _hubService;

    public CommandExecutor(HubConnectionService hubService)
    {
        _hubService = hubService;
    }

    public async Task<CommandResult> ExecuteAsync(string input)
    {
        var parts = ParseCommand(input);
        if (parts.Length == 0) return CommandResult.Failure("Empty command");

        var command = parts[0].ToLowerInvariant();
        var args = parts.Length > 1 ? string.Join(" ", parts[1..]) : "";

        return command switch
        {
            "shell" => await ExecuteShellAsync(args),
            "files" => await ExecuteFilesAsync(args),
            "read" => await ExecuteReadAsync(args),
            "write" => await ExecuteWriteAsync(args),
            "terminal" => await ExecuteTerminalAsync(args),
            "copilot" => await ExecuteCopilotAsync(args),
            "explain" => await ExecuteExplainAsync(args),
            _ => CommandResult.Failure($"Unknown command: {command}")
        };
    }

    private string[] ParseCommand(string input)
    {
        // 간단한 파싱: 따옴표 지원
        var result = new List<string>();
        var current = "";
        var inQuotes = false;
        var quoteChar = '"';

        foreach (var c in input)
        {
            if ((c == '"' || c == '\'') && !inQuotes)
            {
                inQuotes = true;
                quoteChar = c;
            }
            else if (c == quoteChar && inQuotes)
            {
                inQuotes = false;
            }
            else if (c == ' ' && !inQuotes)
            {
                if (!string.IsNullOrEmpty(current))
                {
                    result.Add(current);
                    current = "";
                }
            }
            else
            {
                current += c;
            }
        }

        if (!string.IsNullOrEmpty(current))
            result.Add(current);

        return result.ToArray();
    }

    private async Task<CommandResult> ExecuteShellAsync(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            Console.WriteLine("[error] Usage: shell <command>");
            return CommandResult.Failure("No command provided");
        }

        var result = await _hubService.ExecuteShellAsync(command,
            onOutput: text => Console.Write($"[stdout] {text}"),
            onError: text => Console.Write($"[stderr] {text}"));

        Console.WriteLine($"[exit: {result.ExitCode}]");
        return result;
    }

    private async Task<CommandResult> ExecuteFilesAsync(string path)
    {
        var files = await _hubService.GetFilesAsync(string.IsNullOrEmpty(path) ? null : path);

        if (files.Count == 0)
        {
            Console.WriteLine("(empty or error)");
            return new CommandResult { Success = true, ExitCode = 0 };
        }

        foreach (var file in files)
        {
            if (file.IsDirectory)
            {
                Console.WriteLine($"📁 {file.Name}/");
            }
            else
            {
                var size = FormatSize(file.Size);
                Console.WriteLine($"📄 {file.Name} ({size})");
            }
        }

        return new CommandResult { Success = true, ExitCode = 0, Stdout = $"{files.Count} items" };
    }

    private async Task<CommandResult> ExecuteReadAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("[error] Usage: read <path>");
            return CommandResult.Failure("No path provided");
        }

        var result = await _hubService.ReadFileAsync(path);

        if (result.Success)
        {
            Console.WriteLine(result.Content);
            return new CommandResult { Success = true, ExitCode = 0, Stdout = result.Content };
        }
        else
        {
            Console.WriteLine($"[error] {result.Error}");
            return CommandResult.Failure(result.Error ?? "Read failed");
        }
    }

    private async Task<CommandResult> ExecuteWriteAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("[error] Usage: write <path>");
            return CommandResult.Failure("No path provided");
        }

        Console.WriteLine("Enter content (end with empty line):");
        var lines = new List<string>();
        string? line;
        while (!string.IsNullOrEmpty(line = Console.ReadLine()))
        {
            lines.Add(line);
        }

        var content = string.Join(Environment.NewLine, lines);
        var result = await _hubService.WriteFileAsync(path, content);

        if (result.Success)
        {
            Console.WriteLine($"✓ {result.Message}");
            return new CommandResult { Success = true, ExitCode = 0 };
        }
        else
        {
            Console.WriteLine($"[error] {result.Message}");
            return CommandResult.Failure(result.Message);
        }
    }

    private async Task<CommandResult> ExecuteTerminalAsync(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            Console.WriteLine("[error] Usage: terminal <command>");
            return CommandResult.Failure("No command provided");
        }

        var result = await _hubService.ExecuteTerminalAsync(command);

        if (!string.IsNullOrEmpty(result.Output))
            Console.WriteLine(result.Output);
        if (!string.IsNullOrEmpty(result.Error))
            Console.WriteLine($"[stderr] {result.Error}");

        Console.WriteLine($"[exit: {result.ExitCode}]");

        return new CommandResult
        {
            Success = result.Success,
            Stdout = result.Output,
            Stderr = result.Error,
            ExitCode = result.ExitCode
        };
    }

    private async Task<CommandResult> ExecuteCopilotAsync(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            Console.WriteLine("[error] Usage: copilot <prompt>");
            return CommandResult.Failure("No prompt provided");
        }

        Console.WriteLine("[copilot] Thinking...");
        var result = await _hubService.AskCopilotAsync(prompt,
            onOutput: text => Console.Write(text));

        if (!result.Success)
        {
            Console.WriteLine($"\n[error] {result.Error}");
        }
        else
        {
            Console.WriteLine("\n[complete]");
        }

        return result;
    }

    private async Task<CommandResult> ExecuteExplainAsync(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            Console.WriteLine("[error] Usage: explain <command>");
            return CommandResult.Failure("No command provided");
        }

        Console.WriteLine("[copilot] Explaining...");
        var result = await _hubService.ExplainCommandAsync(command,
            onOutput: text => Console.Write(text));

        if (!result.Success)
        {
            Console.WriteLine($"\n[error] {result.Error}");
        }
        else
        {
            Console.WriteLine("\n[complete]");
        }

        return result;
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.#} {sizes[order]}";
    }
}
