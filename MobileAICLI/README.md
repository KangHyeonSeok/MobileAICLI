# MobileAICLI

A .NET 8 Blazor Server application that provides a mobile-friendly web UI for GitHub Copilot CLI, file browsing/editing, Git operations, and secure terminal command execution.

## Overview

MobileAICLI is designed to enable productive coding from mobile devices during commute times or when away from a workstation. It provides a web-based interface that connects to GitHub Copilot CLI, file systems, and Git repositories, all optimized for mobile touch interfaces.

## Features

### 🤖 GitHub Copilot Integration
- **Streaming Responses**: Real-time AI responses via SignalR
- **Programmatic Mode**: Execute Copilot commands with full tool support
- **Configurable Permissions**: Fine-grained control over tool access
  - File operations (read, write, edit)
  - Git operations (status, commit, push)
  - Shell command execution
- **Model Selection**: Support for multiple AI models (GPT-4, GPT-3.5, Claude 3.5 Sonnet)
- **Chat History**: Maintains conversation context within session
- **Settings Panel**: Toggle tool permissions on-the-fly

### 📁 File Management
- **File Browser**: Navigate directory hierarchies with breadcrumb navigation
- **File Editor**: Edit text files with line numbers
- **Security**: Sandboxed to configured repository path
- **Folder Filtering**: Automatic filtering of hidden and system folders

### 🔀 Git Operations
- **Repository Status**: View current branch, modified files, and staged changes
- **Diff Viewing**: See file changes before committing
- **Stage/Unstage**: Interactive file staging
- **Commit**: Create commits with custom messages
- **Branch Management**: Create, switch, and delete branches
- **Discard Changes**: Revert file modifications
- **Working Directory Selection**: Switch between multiple repositories

### 💻 Terminal
- **Command Execution**: Run whitelisted shell commands
- **Real-time Output**: Streaming command output via SignalR
- **Security**: Command whitelist with dangerous character blocking
- **Environment**: Executes in configured repository context

### ⚙️ Environment Settings
- **Dynamic Configuration**: Modify settings without restarting
- **Path Management**: Configure repository paths and CLI tool locations
- **Command Whitelist**: Add/remove allowed shell commands
- **Password Management**: Change password with secure hashing
- **Audit Logging**: Track all configuration changes

### 🔐 Authentication & Security
- **Password Protection**: PBKDF2 hashing with 100,000 iterations
- **Session Management**: Configurable timeout with sliding expiration
- **Rate Limiting**: Protection against brute-force attacks
- **Audit Logs**: Track authentication events
- **Privacy**: IP address masking in logs

## Architecture

MobileAICLI follows clean architecture principles with clear separation of concerns:

### Layer Structure

```
┌───────────────────────────────────────────┐
│         Presentation Layer                │
│  (Blazor Components + SignalR Hubs)       │
├───────────────────────────────────────────┤
│         Business Logic Layer              │
│  (Services with dependency injection)     │
├───────────────────────────────────────────┤
│         Infrastructure Layer              │
│  (Process execution, File I/O, Git CLI)   │
└───────────────────────────────────────────┘
```

### Service Layer Pattern

All services follow a consistent pattern:

```csharp
public class SomeService
{
    private readonly MobileAICLISettings _settings;
    private readonly ILogger<SomeService> _logger;
    
    public SomeService(
        IOptions<MobileAICLISettings> settings, 
        ILogger<SomeService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }
}
```

### Key Services

| Service | Responsibility |
|---------|---------------|
| `CopilotStreamingService` | Executes Copilot CLI with streaming output |
| `FileService` | File/directory operations with sandboxing |
| `GitService` | Git CLI wrapper for repository operations |
| `TerminalService` | Whitelisted shell command execution |
| `ShellStreamingService` | Real-time command output streaming |
| `AuthService` | Password hashing, verification, and session management |
| `SettingsService` | Dynamic configuration updates |
| `RepositoryContext` | Per-session working directory management |
| `AuditLogService` | Security event logging |
| `ToolDiscoveryService` | Copilot tool capability detection |
| `LocalStorageService` | Browser localStorage abstraction |

### SignalR Hubs

| Hub | Purpose |
|-----|---------|
| `CopilotHub` | Streams Copilot responses to browser |
| `ShellHub` | Streams terminal command output |
| `GitHub` | Handles Git operations |
| `SettingsHub` | Manages settings updates |

## Security Design

### Defense in Depth

1. **Authentication Layer**
   - Password-based with secure hashing (PBKDF2, SHA-256, 100k iterations)
   - Rate limiting on failed attempts
   - Session cookies with HttpOnly, Secure, SameSite flags

2. **Authorization Layer**
   - Command whitelist enforcement
   - Path validation (prevent directory traversal)
   - Tool permission configuration

3. **Input Validation**
   - Dangerous character blocking in commands: `;|&><$\n\r`
   - Relative path validation
   - File extension filtering

4. **Audit & Monitoring**
   - All authentication events logged
   - Settings changes tracked
   - IP addresses masked in logs for privacy

## Configuration

### Settings Model

Configuration is managed through `MobileAICLISettings` bound to the `MobileAICLI` section in `appsettings.json`:

| Property | Type | Description |
|----------|------|-------------|
| `RepositoryPath` | string | Root directory for file operations |
| `GitHubCopilotCommand` | string | Copilot CLI command (default: "copilot") |
| `GitHubCliPath` | string | GitHub CLI path (default: "gh") |
| `GitCliPath` | string | Git CLI path (default: "git") |
| `AllowedShellCommands` | List<string> | Whitelisted terminal commands |
| `AllowedRepositoryRoots` | List<string> | Additional allowed repository roots |
| `AllowedWorkRoots` | List<string> | Allowed work directories (supports wildcards) |
| `EnableAuthentication` | bool | Enable password protection |
| `SessionTimeoutMinutes` | int | Session expiration time |
| `MaxFailedLoginAttempts` | int | Rate limit threshold |
| `FailedLoginDelaySeconds` | int | Delay after failed login |
| `RateLimitResetMinutes` | int | Rate limit reset period |
| `CopilotModel` | string | Default AI model |
| `AllowedCopilotModels` | List<string> | Permitted models for security |

### Runtime Settings Management

Settings can be modified at runtime through the Settings page, which:
- Validates all inputs
- Updates `appsettings.json` atomically
- Logs changes to audit log
- Uses `IOptionsSnapshot` for automatic reload (some settings require app restart)

## Prerequisites

- **.NET 8.0 SDK** or later
- **GitHub CLI** (`gh`) authenticated with your GitHub account
- **GitHub Copilot CLI** (`@github/copilot`) with valid subscription
- **Git** CLI
- **macOS, Linux, or Windows with WSL**

## Running the Application

1. Clone the repository:
   ```bash
   git clone https://github.com/KangHyeonSeok/MobileAICLI.git
   cd MobileAICLI/MobileAICLI
   ```

2. Configure `appsettings.json` with your repository path

3. Run the application:
   ```bash
   dotnet run
   ```

4. Navigate to `http://localhost:5252` (or the URL shown in terminal)

## Development

### Building

```bash
dotnet build
```

### Running Tests

```bash
dotnet test
```

### Development Mode

```bash
dotnet run --environment Development
```

### Project Structure

```
MobileAICLI/
├── Components/
│   ├── Layout/              # NavMenu, MainLayout
│   └── Pages/               # Razor pages (Copilot, Files, Git, etc.)
├── Controllers/             # API controllers (Auth)
├── Hubs/                    # SignalR hubs
├── Models/                  # Configuration and data models
├── Services/                # Business logic services
└── wwwroot/                 # Static assets (CSS, JS, images)
```

## Technologies Used

- **.NET 8**: Latest .NET with performance improvements
- **Blazor Server**: C#-based interactive web UI
- **SignalR**: Real-time bidirectional communication
- **Bootstrap 5**: Responsive CSS framework
- **Bootstrap Icons**: UI icon library
- **xUnit**: Unit testing framework

## License

MIT License

Copyright (c) 2025 KangHyeonSeok

See the [LICENSE](../LICENSE) file for details.
