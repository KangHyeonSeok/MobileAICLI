# MobileAICLI
ai cli를 모바일에서 사용 할 수 있도록 하는 프로젝트

A .NET 8 Blazor Server application that provides a mobile-friendly web UI for AI CLIs (starting with GitHub Copilot CLI), file browsing, editing, and whitelisted terminal command execution.

## Quick Start

```bash
cd MobileAICLI
dotnet run
```

Then navigate to http://localhost:5285

## Features

- 🤖 **GitHub Copilot CLI Integration** - Ask AI to suggest or explain commands
- 📁 **File Browser** - Browse and navigate your repository files
- ✏️ **File Editor** - Edit files directly in the browser
- 💻 **Terminal** - Execute whitelisted shell commands
- 📱 **Mobile-Friendly** - Responsive design optimized for mobile devices
- 🔒 **Secure** - Command whitelisting and path validation

## Configuration

Edit `MobileAICLI/appsettings.json`:

```json
{
  "MobileAICLI": {
    "RepositoryPath": "/path/to/your/repository",
    "GitHubCopilotCommand": "gh copilot",
    "AllowedShellCommands": ["ls", "pwd", "cat", "git status"]
  }
}
```

## Documentation

See [MobileAICLI/README.md](MobileAICLI/README.md) for detailed documentation.

## Requirements

- .NET 8.0 SDK
- GitHub CLI (`gh`) for Copilot features
- macOS or Linux (recommended)
