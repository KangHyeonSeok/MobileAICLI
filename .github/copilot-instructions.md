# MobileAICLI - AI Coding Instructions

## Project Overview
A .NET 8 Blazor Server app providing mobile-friendly web UI for GitHub Copilot CLI, file browsing, and secure terminal command execution. Runs on `http://0.0.0.0:5252`.

## 📚 Documentation Structure

**문서 작성/수정 시 반드시 참조**: [DOCUMENTATION_GUIDELINES.md](../docs/DOCUMENTATION_GUIDELINES.md)

| 문서 | 설명 |
|------|------|
| [DOCUMENTATION_GUIDELINES.md](../docs/DOCUMENTATION_GUIDELINES.md) | 문서 작성 지침 (필독) |
| [01_COPILOT_INTEGRATION.md](../docs/features/01_COPILOT_INTEGRATION.md) | Copilot 연동 기능 설계 |
| [COPILOT_INTEGRATION_DESIGN.md](../docs/technical/COPILOT_INTEGRATION_DESIGN.md) | Copilot 기술 설계 |
| [TEST_CLIENT_DESIGN.md](../docs/technical/TEST_CLIENT_DESIGN.md) | 테스트 클라이언트 설계 |

### 문서 작성 원칙
- **코드는 AI가 생성** → 문서에는 설계와 의사결정만 포함
- **20줄 이상 코드 블록 금지** → 제약 조건과 맥락만 제공
- **features/**: 비개발자도 이해 가능한 기능 설명
- **technical/**: AI가 참조할 설계 방향, 제약 조건, 아키텍처

## Architecture

### Core Pattern: Service Layer + Blazor Pages
- **Services** (`Services/`): Business logic with `IOptions<MobileAICLISettings>` dependency injection
  - `CopilotService`: Wraps `copilot -p "prompt"` via `Process.Start()` (Programmatic Mode)
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
- **개인정보 보호 (필수)**:
  - ❌ 사용자 이름, 경로, IP 주소 등 개인정보가 포함된 코드/설명/구문 작성 금지
  - ❌ API 키, 토큰, 비밀번호 등 민감 정보 하드코딩 금지
  - ❌ 개인정보가 포함된 커밋 금지
  - ✅ 환경변수 또는 설정 파일을 통해 민감 정보 관리
  - ✅ 예시 경로는 `/path/to/repo`, `~/Documents` 등 일반적 표현 사용
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

# Run tests
dotnet test
```

### Unit Testing
- **가능하면 단위 테스트 작성**: 새로운 서비스나 기능 구현 시 테스트 코드 함께 작성
- **테스트 프로젝트**: `MobileAICLI.Tests/` (xUnit)
- **네이밍 규칙**: `{ClassName}Tests.cs`, 메서드명 `{Method}_{Scenario}_{ExpectedResult}`
- **Mocking**: 외부 의존성(Process, FileSystem)은 인터페이스로 추상화하여 테스트 용이하게

## Configuration
Edit `MobileAICLI/appsettings.json`:
```json
{
  "MobileAICLI": {
    "RepositoryPath": "/path/to/repo",
    "GitHubCopilotCommand": "copilot",
    "AllowedShellCommands": ["ls", "pwd", "git status"]
  }
}
```

## File Naming
- Services: `*Service.cs` in `Services/`
- Pages: `*.razor` in `Components/Pages/`
- Models: `*Settings.cs` in `Models/`
- Feature Docs: `docs/features/NN_FEATURE_NAME.md`
- Technical Docs: `docs/technical/FEATURE_DESIGN.md`
