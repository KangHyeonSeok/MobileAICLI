using System.CommandLine;
using MobileAICLI.TestClient.Services;
using MobileAICLI.TestClient.Testing;

namespace MobileAICLI.TestClient;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var serverUrlArgument = new Argument<string>(
            name: "serverUrl",
            description: "MobileAICLI server URL (e.g., http://localhost:5252)",
            getDefaultValue: () => "http://localhost:5252");

        var execOption = new Option<string?>(
            name: "--exec",
            description: "Execute a single command and exit");

        var scriptOption = new Option<string?>(
            name: "--script",
            description: "Execute commands from a test script file");

        var jsonOption = new Option<bool>(
            name: "--json",
            description: "Output results in JSON format");

        var stdinOption = new Option<bool>(
            name: "--stdin",
            description: "Read commands from standard input");

        var rootCommand = new RootCommand("MobileAICLI Test Client - SignalR-based integration testing tool")
        {
            serverUrlArgument,
            execOption,
            scriptOption,
            jsonOption,
            stdinOption
        };

        rootCommand.SetHandler(async (serverUrl, exec, script, json, stdin) =>
        {
            await RunAsync(serverUrl, exec, script, json, stdin);
        }, serverUrlArgument, execOption, scriptOption, jsonOption, stdinOption);

        return await rootCommand.InvokeAsync(args);
    }

    static async Task RunAsync(string serverUrl, string? exec, string? script, bool json, bool stdin)
    {
        var hubService = new HubConnectionService(serverUrl);

        Console.WriteLine($"Connecting to {serverUrl}/testhub...");
        
        if (!await hubService.ConnectAsync())
        {
            Console.Error.WriteLine("Failed to connect to server");
            Environment.Exit(2);
            return;
        }

        Console.WriteLine("✓ Connected\n");

        // Script mode
        if (!string.IsNullOrEmpty(script))
        {
            var runner = new TestRunner(hubService, json);
            var exitCode = await runner.RunScriptAsync(script);
            await hubService.DisconnectAsync();
            Environment.Exit(exitCode);
            return;
        }

        // Single exec mode
        if (!string.IsNullOrEmpty(exec))
        {
            var runner = new TestRunner(hubService, json);
            var exitCode = await runner.RunSingleCommandAsync(exec);
            await hubService.DisconnectAsync();
            Environment.Exit(exitCode);
            return;
        }

        // Stdin mode
        if (stdin)
        {
            var runner = new TestRunner(hubService, json);
            var commands = new List<string>();
            string? line;
            while ((line = Console.ReadLine()) != null)
            {
                commands.Add(line);
            }
            var exitCode = await runner.RunCommandsAsync(commands);
            await hubService.DisconnectAsync();
            Environment.Exit(exitCode);
            return;
        }

        // Interactive REPL mode
        await RunInteractiveAsync(hubService);
        await hubService.DisconnectAsync();
    }

    static async Task RunInteractiveAsync(HubConnectionService hubService)
    {
        var executor = new CommandExecutor(hubService);

        Console.WriteLine("MobileAICLI Test Client - Interactive Mode");
        Console.WriteLine("Type 'help' for available commands, 'exit' to quit\n");

        while (true)
        {
            Console.Write("MobileAICLI> ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("quit", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Goodbye!");
                break;
            }

            if (input.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                PrintHelp();
                continue;
            }

            if (input.Equals("clear", StringComparison.OrdinalIgnoreCase))
            {
                Console.Clear();
                continue;
            }

            if (input.Equals("status", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(hubService.IsConnected ? "✓ Connected" : "✗ Disconnected");
                continue;
            }

            await executor.ExecuteAsync(input);
            Console.WriteLine();
        }
    }

    static void PrintHelp()
    {
        Console.WriteLine(@"
Available Commands:
───────────────────────────────────────────────────
  shell <command>     Execute a shell command (streaming)
  files [path]        List files in directory
  read <path>         Read file content
  write <path>        Write to file (prompts for content)
  terminal <command>  Execute whitelisted terminal command
  copilot <prompt>    Ask GitHub Copilot
  explain <command>   Explain a command using Copilot

Special Commands:
───────────────────────────────────────────────────
  help                Show this help message
  status              Show connection status
  clear               Clear the screen
  exit                Exit the program
");
    }
}
