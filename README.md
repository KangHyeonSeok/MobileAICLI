# MobileAICLI

AI CLI를 모바일에서 remote로 사용할 수 있도록 하는 프로젝트  
출퇴근 시간이 아까워서 휴대폰으로 바이브 코딩하고 싶어서 만들기 시작함.

A .NET 8 Blazor Server application that provides a mobile-friendly web UI for GitHub Copilot CLI, file browsing/editing, Git operations, and secure terminal command execution.

## Quick Start

```bash
cd MobileAICLI
dotnet run
```

Then navigate to **http://localhost:5252**

**최초 비밀번호**: `admin`

## Features

- 🤖 **GitHub Copilot CLI Integration** - AI-powered coding assistant with streaming responses
  - Programmatic mode with real-time streaming via SignalR
  - Configurable tool permissions (file operations, Git commands, shell execution)
  - Model selection support (GPT-4, GPT-3.5, Claude 3.5 Sonnet)
  - Chat history with context retention
- 📁 **File Browser** - Browse and navigate repository files with folder hierarchy
- ✏️ **File Editor** - Edit files directly in the browser with syntax awareness
- 🔀 **Git Integration** - Comprehensive Git operations
  - Status viewing and diff inspection
  - Stage/unstage files
  - Commit with messages
  - Branch management (create, switch, delete)
  - Discard changes
- 💻 **Terminal** - Execute whitelisted shell commands with real-time output
- ⚙️ **Environment Settings** - Dynamic configuration management
  - Repository path and CLI tool paths
  - Allowed shell commands whitelist
  - Password management with secure hashing
  - Audit logging for all setting changes
- 🔐 **Authentication** - Secure password-based authentication
  - PBKDF2 password hashing with SHA-256
  - Session management with configurable timeout
  - Rate limiting on failed login attempts
  - IP address masking in logs for privacy
- 📱 **Mobile-Friendly** - Responsive design optimized for mobile devices
- 🔒 **Security** - Command whitelisting, path validation, and sandboxed execution

## Configuration

### Application Settings

Edit `MobileAICLI/appsettings.json`:

```json
{
  "MobileAICLI": {
    "RepositoryPath": "/path/to/your/repository",
    "GitHubCopilotCommand": "copilot",
    "GitHubCliPath": "gh",
    "GitCliPath": "git",
    "AllowedShellCommands": [
      "ls", "pwd", "cd", "cat", "echo",
      "git status", "git log", "git diff"
    ],
    "AllowedRepositoryRoots": [],
    "AllowedWorkRoots": [],
    "EnableAuthentication": true,
    "SessionTimeoutMinutes": 30,
    "MaxFailedLoginAttempts": 5,
    "FailedLoginDelaySeconds": 1,
    "RateLimitResetMinutes": 15,
    "CopilotModel": "default",
    "AllowedCopilotModels": [
      "default",
      "gpt-4",
      "gpt-3.5-turbo",
      "claude-3.5-sonnet"
    ]
  }
}
```

### Configuration Options

| Setting | Description | Default |
|---------|-------------|---------|
| `RepositoryPath` | Root directory for file operations and command execution | Required |
| `GitHubCopilotCommand` | Command to invoke GitHub Copilot CLI | `copilot` |
| `GitHubCliPath` | Path to GitHub CLI executable | `gh` |
| `GitCliPath` | Path to Git executable | `git` |
| `AllowedShellCommands` | Whitelist of shell commands allowed in Terminal | `[]` |
| `AllowedRepositoryRoots` | Additional allowed repository roots | `[]` |
| `AllowedWorkRoots` | Allowed working directory roots with wildcard support | `[]` |
| `EnableAuthentication` | Enable/disable password authentication | `true` |
| `SessionTimeoutMinutes` | Session expiration time | `30` |
| `MaxFailedLoginAttempts` | Max failed login attempts before rate limiting | `5` |
| `FailedLoginDelaySeconds` | Delay after failed login | `1` |
| `RateLimitResetMinutes` | Rate limit reset time | `15` |
| `CopilotModel` | Default Copilot model | `default` |
| `AllowedCopilotModels` | Allowed Copilot models for security | See above |

### Environment Variables

For security, the password hash must be set via environment variable:

```bash
export MOBILEAICLI_PASSWORD_HASH='pbkdf2$100000$salt$hash'
```

**⚠️ Never commit password hashes to `appsettings.json`!**

To generate a password hash, you can use the Settings page after first login or create one programmatically using the `AuthService.HashPassword()` method.

## Requirements

- **.NET 8.0 SDK** or later
- **GitHub CLI** (`gh`) - For Copilot and Git operations
  ```bash
  # macOS
  brew install gh
  
  # Linux/WSL
  # See https://github.com/cli/cli/blob/trunk/docs/install_linux.md
  # Or use: curl -sS https://webinstall.dev/gh | bash
  
  gh auth login
  ```
- **GitHub Copilot CLI** - For AI assistant features
  ```bash
  npm install -g @github/copilot
  copilot auth login
  ```
- **Git** - Usually pre-installed on macOS/Linux
- **macOS or Linux** (recommended) - Windows WSL also supported

## Installation & Running

1. **Clone the repository**:
   ```bash
   git clone https://github.com/KangHyeonSeok/MobileAICLI.git
   cd MobileAICLI
   ```

2. **Configure settings**:
   - Edit `MobileAICLI/appsettings.json` with your repository path and preferences
   - Set `MOBILEAICLI_PASSWORD_HASH` environment variable (optional, defaults to "admin")

3. **Run the application**:
   ```bash
   cd MobileAICLI
   dotnet run
   ```

4. **Access the application**:
   - Open your browser to **http://localhost:5252**
   - Login with password (default: `admin`)
   - Change the default password in Settings page for security

## Development

**Build the project**:
```bash
dotnet build
```

**Run tests**:
```bash
dotnet test
```

**Run in development mode**:
```bash
dotnet run --environment Development
```

## Security Considerations

- **Authentication**: Password-based authentication with PBKDF2 hashing (100,000 iterations, SHA-256)
- **Session Management**: Configurable timeout with sliding expiration
- **Rate Limiting**: Failed login attempts are rate-limited
- **Terminal Commands**: Only whitelisted commands can be executed
- **File Access**: Operations are restricted to configured `RepositoryPath` and cannot escape the sandbox
- **Path Validation**: All file paths are validated to prevent directory traversal attacks
- **Command Sanitization**: Dangerous characters (`;|&><$`) are blocked in terminal commands
- **Privacy**: IP addresses in logs are masked (shows only first two octets)
- **Audit Logging**: All critical operations are logged for security review

## Architecture

MobileAICLI follows a clean architecture pattern with clear separation of concerns:

```
┌─────────────────────────────────────────────────────────┐
│                    Browser (Client)                      │
│  ┌────────────────────────────────────────────────┐    │
│  │   Blazor Components (.razor)                   │    │
│  │   - Interactive Server Mode                    │    │
│  │   - SignalR for real-time communication        │    │
│  └────────────────┬───────────────────────────────┘    │
└───────────────────┼──────────────────────────────────────┘
                    │ SignalR WebSocket
┌───────────────────▼──────────────────────────────────────┐
│                Blazor Server (.NET 8)                    │
│  ┌──────────────────────────────────────────────────┐   │
│  │              SignalR Hubs                        │   │
│  │  - CopilotHub, ShellHub, GitHub, SettingsHub   │   │
│  └───────────────┬──────────────────────────────────┘   │
│                  │                                       │
│  ┌───────────────▼──────────────────────────────────┐   │
│  │              Services Layer                      │   │
│  │  - CopilotStreamingService                       │   │
│  │  - FileService, GitService                       │   │
│  │  - TerminalService, AuthService                  │   │
│  │  - RepositoryContext (scoped per session)        │   │
│  └───────────────┬──────────────────────────────────┘   │
│                  │                                       │
│  ┌───────────────▼──────────────────────────────────┐   │
│  │         External Process Execution               │   │
│  │  - copilot CLI (streaming)                       │   │
│  │  - git CLI                                       │   │
│  │  - shell commands (whitelisted)                  │   │
│  └──────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────┘
```

### Key Design Patterns

- **Service Layer Pattern**: Business logic encapsulated in reusable services
- **Dependency Injection**: Services configured with `IOptions<MobileAICLISettings>`
- **Repository Context**: Scoped service manages working directory per session
- **SignalR Streaming**: Real-time bidirectional communication for long-running operations
- **Process Streaming**: Async streaming of CLI output to browser in real-time

## Documentation

Detailed documentation available in the `docs/` directory:

- **Features**: User-facing feature documentation
  - [Authentication](docs/features_implemented/AUTHENTICATION.md)
  - [Environment Settings](docs/features_implemented/03_ENVIRONMENT_SETTINGS.md)
  
- **Technical**: Architecture and design documents
  - [Copilot Integration Design](docs/technical_implemented/COPILOT_INTEGRATION_DESIGN.md)
  - [Authentication Design](docs/technical_implemented/AUTHENTICATION_DESIGN.md)
  - [Working Directory Management](docs/technical_implemented/WORKING_DIRECTORY_MANAGEMENT.md)

See also: [MobileAICLI/README.md](MobileAICLI/README.md) for detailed technical documentation.

## Project Structure

```
MobileAICLI/
├── Components/              # Blazor components
│   ├── Layout/             # Layout components (NavMenu, MainLayout)
│   └── Pages/              # Page components
│       ├── Copilot.razor          # AI assistant chat interface
│       ├── FileBrowser.razor      # File browser and editor
│       ├── Git.razor              # Git operations UI
│       ├── Terminal.razor         # Terminal command execution
│       ├── Settings.razor         # Environment settings management
│       └── Login.razor            # Authentication
├── Controllers/            # API controllers
│   └── AuthController.cs          # Authentication endpoints
├── Hubs/                   # SignalR hubs
│   ├── CopilotHub.cs              # Copilot streaming
│   ├── ShellHub.cs                # Terminal streaming
│   ├── GitHub.cs                  # Git operations
│   └── SettingsHub.cs             # Settings management
├── Models/                 # Data models
│   ├── MobileAICLISettings.cs     # Configuration model
│   ├── CopilotSettings.cs         # Copilot tool settings
│   └── Git*.cs                    # Git-related models
├── Services/               # Business logic services
│   ├── CopilotStreamingService.cs # Copilot CLI integration
│   ├── FileService.cs             # File operations
│   ├── GitService.cs              # Git operations
│   ├── TerminalService.cs         # Shell command execution
│   ├── AuthService.cs             # Authentication logic
│   ├── SettingsService.cs         # Settings management
│   ├── RepositoryContext.cs       # Working directory management
│   └── AuditLogService.cs         # Audit logging
├── wwwroot/                # Static files
│   ├── css/                       # Stylesheets
│   ├── js/                        # JavaScript helpers
│   └── images/                    # Images
├── appsettings.json        # Application configuration
└── Program.cs              # Application entry point

MobileAICLI.Tests/          # Unit tests
MobileAICLI.TestClient/     # Test client for Copilot integration

docs/                       # Documentation
├── features_implemented/   # User-facing features
└── technical_implemented/  # Technical design documents
```

## Technologies Used

- **.NET 8**: Latest version of .NET with enhanced performance
- **Blazor Server**: Interactive web UI with C# (no JavaScript SPA framework needed)
- **SignalR**: Real-time bidirectional communication via WebSockets
- **Bootstrap 5**: Responsive CSS framework
- **Bootstrap Icons**: Icon library for UI elements
- **PBKDF2**: Secure password hashing algorithm

## License

MIT License - See the [LICENSE](LICENSE) file for details.

Copyright (c) 2025 KangHyeonSeok

## Contributing

Contributions are welcome! Please feel free to submit issues or pull requests.

## Roadmap

- [ ] Interactive Copilot mode with conversation context
- [ ] Pull request creation from Git page
- [ ] File upload/download capabilities
- [ ] Syntax highlighting in file editor
- [ ] Dark mode theme
- [ ] Multi-repository support
- [ ] WebSocket compression for better mobile performance
