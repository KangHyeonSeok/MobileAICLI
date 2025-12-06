# MobileAICLI

A .NET 8 Blazor Server application that provides a mobile-friendly web UI for AI CLIs (starting with GitHub Copilot CLI), file browsing, editing, and whitelisted terminal command execution.

## Features

- **GitHub Copilot CLI Integration**: Ask Copilot to suggest commands or explain existing ones
- **File Browser**: Browse files and directories in your configured repository
- **File Editor**: Edit files directly in the browser with a simple text editor
- **Terminal**: Execute whitelisted shell commands in your repository
- **Mobile-Friendly**: Responsive design optimized for mobile devices

## Prerequisites

- .NET 8.0 SDK or later
- GitHub CLI (`gh`) installed (for Copilot features)
- macOS or Linux (recommended)

## Configuration

Configure the application by editing `appsettings.json`:

```json
{
  "MobileAICLI": {
    "RepositoryPath": "/path/to/your/repository",
    "GitHubCopilotCommand": "gh copilot",
    "AllowedShellCommands": [
      "ls",
      "pwd",
      "cd",
      "cat",
      "echo",
      "git status",
      "git log",
      "git diff"
    ]
  }
}
```

### Configuration Options

- **RepositoryPath**: The root directory where files will be browsed and commands will be executed
- **GitHubCopilotCommand**: The command to invoke GitHub Copilot CLI (default: `gh copilot`)
- **AllowedShellCommands**: A whitelist of shell commands that can be executed via the Terminal page

## Running the Application

1. Clone the repository:
   ```bash
   git clone https://github.com/KangHyeonSeok/MobileAICLI.git
   cd MobileAICLI/MobileAICLI
   ```

2. Configure your settings in `appsettings.json`

3. Run the application:
   ```bash
   dotnet run
   ```

4. Open your browser and navigate to `http://localhost:5285` (or the URL shown in the terminal)

## Development

Build the project:
```bash
dotnet build
```

Run in development mode:
```bash
dotnet run --environment Development
```

## Security Considerations

- **Terminal Commands**: Only whitelisted commands can be executed. Modify `AllowedShellCommands` in `appsettings.json` to add or remove allowed commands.
- **File Access**: The file browser and editor are restricted to the configured `RepositoryPath` and cannot access files outside this directory.
- **GitHub Copilot**: Requires GitHub CLI authentication and Copilot access.

## Project Structure

```
MobileAICLI/
├── Components/          # Blazor components
│   ├── Layout/         # Layout components (navigation, main layout)
│   └── Pages/          # Page components (Home, Copilot, Files, Terminal)
├── Models/             # Configuration models
├── Services/           # Business logic services
│   ├── CopilotService.cs
│   ├── FileService.cs
│   └── TerminalService.cs
├── wwwroot/            # Static files (CSS, images)
├── appsettings.json    # Application configuration
└── Program.cs          # Application entry point
```

## Technologies Used

- **.NET 8**: Latest version of .NET
- **Blazor Server**: For interactive web UI with C#
- **Bootstrap 5**: For responsive design
- **Bootstrap Icons**: For UI icons

## License

See the [LICENSE](../LICENSE) file for details.
