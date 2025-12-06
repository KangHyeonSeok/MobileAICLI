# MobileAICLI - AI Coding Instructions

## Project Overview
A .NET 8 Blazor Server app providing mobile-friendly web UI for GitHub Copilot CLI, file browsing, and secure terminal command execution. Runs on `http://localhost:5285`.

## Architecture

### Core Pattern: Service Layer + Blazor Pages
- **Services** (`Services/`): Business logic with `IOptions<MobileAICLISettings>` dependency injection
  - `CopilotService`: Wraps `gh copilot suggest/explain` via `Process.Start()`
  - `FileService`: Sandboxed file operations within `RepositoryPath`
  - `TerminalService`: Whitelisted command execution with security validation
- **Pages** (`Components/Pages/`): Blazor components with `@rendermode InteractiveServer`
- **Configuration**: Settings bound to `MobileAICLI` section in `appsettings.json`

### Data Flow
```
Blazor Page (@inject Service) → Service (IOptions<Settings>) → Process/FileSystem
                              ↓
                    MobileAICLISettings (from appsettings.json)
```

## Key Conventions

### Service Implementation Pattern
All services follow this structure (see `Services/FileService.cs`):
```csharp
public class XxxService
{
    private readonly MobileAICLISettings _settings;
    private readonly ILogger<XxxService> _logger;
    
    public XxxService(IOptions<MobileAICLISettings> settings, ILogger<XxxService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }
}
```

### Security Patterns (Critical)
- **Path Validation**: Always use `Path.GetRelativePath()` to verify paths don't escape `RepositoryPath`:
  ```csharp
  var relPath = Path.GetRelativePath(repoPath, fullPath);
  if (relPath.StartsWith("..") || Path.IsPathRooted(relPath)) return;
  ```
- **Command Whitelist**: Terminal commands must be prefix-matched against `AllowedShellCommands`
- **Dangerous Character Blocking**: Block `;|&><`$\n\r` in terminal commands

### Blazor Component Pattern
- Use `@rendermode InteractiveServer` directive
- Inject services with `@inject ServiceName ServiceName`
- Return tuples `(bool Success, string Output, string Error)` from async service methods

## Developer Workflow

```bash
# Run (from MobileAICLI/ subdirectory)
cd MobileAICLI && dotnet run

# Build
dotnet build

# Development mode
dotnet run --environment Development
```

## Configuration
Edit `MobileAICLI/appsettings.json`:
```json
{
  "MobileAICLI": {
    "RepositoryPath": "/path/to/repo",
    "GitHubCopilotCommand": "gh copilot",
    "AllowedShellCommands": ["ls", "pwd", "git status"]
  }
}
```

## File Naming
- Services: `*Service.cs` in `Services/`
- Pages: `*.razor` in `Components/Pages/`
- Models: `*Settings.cs` in `Models/`
