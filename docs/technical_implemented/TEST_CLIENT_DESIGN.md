# 테스트 콘솔 클라이언트 설계

## 목적

SignalR Hub를 통해 MobileAICLI의 모든 기능을 테스트할 수 있는 콘솔 클라이언트 개발.

- 브라우저 없이 기능 검증
- 자동화된 테스트 시나리오 실행
- 개발 중 빠른 피드백 루프

---

## 아키텍처

```
┌─────────────────────────────────────────────────────────────┐
│                    MobileAICLI Server                        │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │ FileService │  │CopilotService│  │ShellStreamingService│  │
│  └──────┬──────┘  └──────┬──────┘  └──────────┬──────────┘  │
│         │                │                     │             │
│         └────────────────┼─────────────────────┘             │
│                          │                                   │
│                   ┌──────▼──────┐                           │
│                   │   TestHub   │  ← 통합 SignalR Hub        │
│                   └──────┬──────┘                           │
│                          │ /testhub                         │
└──────────────────────────┼──────────────────────────────────┘
                           │ SignalR WebSocket
                           │
┌──────────────────────────▼──────────────────────────────────┐
│              MobileAICLI.TestClient (Console)               │
│  ┌─────────────────────────────────────────────────────┐   │
│  │                  Interactive REPL                    │   │
│  │  > shell echo hello                                  │   │
│  │  > copilot "list files in current directory"        │   │
│  │  > files /                                           │   │
│  │  > read README.md                                    │   │
│  └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

---

## 구현 범위

### Phase 1: 기본 인프라

| 항목 | 설명 |
|------|------|
| TestHub | 모든 서비스를 노출하는 통합 Hub |
| TestClient 프로젝트 | .NET 8 콘솔 앱 |
| 연결 관리 | 자동 재연결, 상태 표시 |

### Phase 2: 명령어 구현

| 명령어 | 서비스 | 응답 방식 |
|--------|--------|-----------|
| `shell <cmd>` | ShellStreamingService | 스트리밍 |
| `copilot <prompt>` | CopilotService | 스트리밍 |
| `explain <cmd>` | CopilotService | 스트리밍 |
| `files [path]` | FileService | 즉시 응답 |
| `read <path>` | FileService | 즉시 응답 |
| `write <path>` | FileService | 즉시 응답 (stdin에서 내용 입력) |
| `terminal <cmd>` | TerminalService | 즉시 응답 |

### Phase 3: AI 자동화 테스트 모드

AI가 테스트를 자동 실행하고 결과를 파싱할 수 있는 기능:

| 기능 | 설명 |
|------|------|
| 스크립트 모드 | `--script test.txt` 또는 `--exec "command"` |
| JSON 출력 | `--json` 으로 파싱 가능한 결과 |
| 검증 문법 | `EXPECT`, `EXPECT_CONTAINS`, `EXPECT_EXIT` |
| 종합 리포트 | 성공/실패 개수, 오류 상세 내용 |

---

## 프로젝트 구조

```
MobileAICLI.sln
├── MobileAICLI/                      # 기존 서버
│   └── Hubs/
│       ├── ShellHub.cs               # 기존 (Shell 전용)
│       └── TestHub.cs                # 신규 (통합 테스트용)
│
└── MobileAICLI.TestClient/           # 신규 콘솔 앱
    ├── MobileAICLI.TestClient.csproj
    ├── Program.cs                    # 엔트리포인트 + CLI 파싱
    ├── Commands/
    │   ├── ICommand.cs               # 명령어 인터페이스
    │   ├── ShellCommand.cs
    │   ├── CopilotCommand.cs
    │   ├── FilesCommand.cs
    │   ├── ReadCommand.cs
    │   └── TerminalCommand.cs
    ├── Testing/
    │   ├── TestScript.cs             # 스크립트 파서
    │   ├── TestCase.cs               # 테스트 케이스 모델
    │   ├── Assertion.cs              # 검증 로직
    │   ├── TestRunner.cs             # 테스트 실행기
    │   └── TestReporter.cs           # 결과 리포터 (Console/JSON)
    └── Services/
        └── HubConnectionService.cs   # SignalR 연결 관리
```

---

## TestHub 설계

```csharp
public class TestHub : Hub
{
    // === Shell (스트리밍) ===
    // Client → Server: ExecuteShell(command)
    // Server → Client: ReceiveShellOutput(text), ReceiveShellError(text), ShellComplete(exitCode)
    
    // === Copilot (스트리밍) ===
    // Client → Server: AskCopilot(prompt), ExplainCommand(command)
    // Server → Client: ReceiveCopilotOutput(text), CopilotComplete(success, error)
    
    // === Files (Request-Response) ===
    // Client → Server: GetFiles(path?) → List<FileItem>
    // Client → Server: ReadFile(path) → FileResult
    // Client → Server: WriteFile(path, content) → WriteResult
    
    // === Terminal (Request-Response) ===
    // Client → Server: ExecuteTerminal(command) → TerminalResult
}
```

---

## 콘솔 클라이언트 UX

### 연결

```
$ MobileAICLI.TestClient http://localhost:5252
Connecting to http://localhost:5252/testhub...
✓ Connected

MobileAICLI>
```

### 명령어 실행 예시

```
MobileAICLI> shell echo "Hello World"
[stdout] Hello World
[exit: 0]

MobileAICLI> files
📁 Components/
📁 Services/
📁 Models/
📄 Program.cs (2.1 KB)
📄 appsettings.json (0.5 KB)

MobileAICLI> read appsettings.json
{
  "MobileAICLI": {
    "RepositoryPath": "/path/to/repo",
    ...
  }
}

MobileAICLI> copilot "list all C# files"
[copilot] To list all C# files, use:
[copilot] find . -name "*.cs"
[complete]

MobileAICLI> terminal git status
On branch main
nothing to commit, working tree clean
[exit: 0]
```

### 특수 명령어

```
MobileAICLI> help              # 도움말
MobileAICLI> status            # 연결 상태
MobileAICLI> clear             # 화면 지우기
MobileAICLI> exit              # 종료
```

---

## AI 자동화 테스트 모드

### 실행 방법

```bash
# 1. 단일 명령어 실행
MobileAICLI.TestClient http://localhost:5252 --exec "shell echo hello"

# 2. 스크립트 파일 실행
MobileAICLI.TestClient http://localhost:5252 --script tests/basic.txt

# 3. JSON 출력 (AI 파싱용)
MobileAICLI.TestClient http://localhost:5252 --script tests/basic.txt --json

# 4. 파이프라인 입력
echo "shell echo hello" | MobileAICLI.TestClient http://localhost:5252 --stdin
```

### 테스트 스크립트 문법 (`.txt` 파일)

```bash
# tests/basic.txt - 기본 테스트 시나리오
# '#'으로 시작하는 줄은 주석

# === 테스트 1: Shell echo ===
TEST shell_echo
shell echo "Hello World"
EXPECT_EXIT 0
EXPECT_CONTAINS Hello World

# === 테스트 2: 파일 목록 ===
TEST list_files
files /
EXPECT_EXIT 0
EXPECT_CONTAINS Program.cs

# === 테스트 3: 파일 읽기 ===
TEST read_file
read appsettings.json
EXPECT_EXIT 0
EXPECT_CONTAINS MobileAICLI
EXPECT_CONTAINS RepositoryPath

# === 테스트 4: 존재하지 않는 파일 ===
TEST read_nonexistent
read nonexistent.txt
EXPECT_EXIT 1
EXPECT_ERROR Access denied

# === 테스트 5: Shell 스트리밍 (ping) ===
TEST shell_ping
shell ping -n 2 localhost
EXPECT_EXIT 0
EXPECT_CONTAINS Reply from
TIMEOUT 10

# === 테스트 6: Terminal 화이트리스트 ===
TEST terminal_allowed
terminal git status
EXPECT_EXIT 0

# === 테스트 7: Terminal 차단 ===
TEST terminal_blocked
terminal rm -rf /
EXPECT_EXIT 1
EXPECT_ERROR not allowed
```

### 검증 지시문

| 지시문 | 설명 | 예시 |
|--------|------|------|
| `TEST <name>` | 테스트 케이스 시작 | `TEST shell_echo` |
| `EXPECT_EXIT <code>` | 종료 코드 검증 | `EXPECT_EXIT 0` |
| `EXPECT_CONTAINS <text>` | 출력에 텍스트 포함 | `EXPECT_CONTAINS Hello` |
| `EXPECT_NOT_CONTAINS <text>` | 출력에 텍스트 미포함 | `EXPECT_NOT_CONTAINS Error` |
| `EXPECT_ERROR <text>` | 에러 출력 검증 | `EXPECT_ERROR not allowed` |
| `EXPECT_REGEX <pattern>` | 정규식 매칭 | `EXPECT_REGEX \d+ files` |
| `TIMEOUT <seconds>` | 타임아웃 설정 | `TIMEOUT 30` |
| `SKIP` | 테스트 건너뛰기 | `SKIP` |

### JSON 출력 형식 (`--json`)

```json
{
  "serverUrl": "http://localhost:5252",
  "scriptFile": "tests/basic.txt",
  "startTime": "2025-12-06T10:30:00Z",
  "endTime": "2025-12-06T10:30:05Z",
  "summary": {
    "total": 7,
    "passed": 6,
    "failed": 1,
    "skipped": 0
  },
  "tests": [
    {
      "name": "shell_echo",
      "status": "passed",
      "duration": 120,
      "command": "shell echo \"Hello World\"",
      "exitCode": 0,
      "stdout": "Hello World\n",
      "stderr": "",
      "assertions": [
        { "type": "EXPECT_EXIT", "expected": "0", "actual": "0", "passed": true },
        { "type": "EXPECT_CONTAINS", "expected": "Hello World", "passed": true }
      ]
    },
    {
      "name": "read_nonexistent",
      "status": "failed",
      "duration": 50,
      "command": "read nonexistent.txt",
      "exitCode": 1,
      "stdout": "",
      "stderr": "File not found",
      "assertions": [
        { "type": "EXPECT_EXIT", "expected": "1", "actual": "1", "passed": true },
        { "type": "EXPECT_ERROR", "expected": "Access denied", "actual": "File not found", "passed": false }
      ],
      "failureReason": "EXPECT_ERROR failed: expected 'Access denied' but got 'File not found'"
    }
  ]
}
```

### 콘솔 출력 형식 (기본)

```
╔══════════════════════════════════════════════════════════════╗
║  MobileAICLI Test Runner                                     ║
║  Server: http://localhost:5252                               ║
║  Script: tests/basic.txt                                     ║
╚══════════════════════════════════════════════════════════════╝

[1/7] shell_echo ......................................... ✓ PASS (120ms)
[2/7] list_files ......................................... ✓ PASS (85ms)
[3/7] read_file .......................................... ✓ PASS (45ms)
[4/7] read_nonexistent ................................... ✗ FAIL (50ms)
      │ Command: read nonexistent.txt
      │ Expected error: "Access denied"
      │ Actual error: "File not found"
[5/7] shell_ping ......................................... ✓ PASS (2100ms)
[6/7] terminal_allowed ................................... ✓ PASS (90ms)
[7/7] terminal_blocked ................................... ✓ PASS (30ms)

══════════════════════════════════════════════════════════════
 RESULTS: 6 passed, 1 failed, 0 skipped (Total: 2.5s)
══════════════════════════════════════════════════════════════

Exit code: 1 (has failures)
```

### AI 사용 시나리오

```bash
# AI가 테스트 스크립트 생성 후 실행
$ cat > /tmp/ai_test.txt << 'EOF'
TEST verify_shell
shell echo "AI Test"
EXPECT_EXIT 0
EXPECT_CONTAINS AI Test
EOF

$ MobileAICLI.TestClient http://localhost:5252 --script /tmp/ai_test.txt --json

# AI가 JSON 결과를 파싱하여 성공/실패 판단
```

### 종료 코드

| 코드 | 의미 |
|------|------|
| 0 | 모든 테스트 통과 |
| 1 | 하나 이상의 테스트 실패 |
| 2 | 연결 오류 |
| 3 | 스크립트 파싱 오류 |

---

## 구현 순서

```
1. TestHub 생성 (서버)
   └── 기존 서비스들 주입
   └── 메서드 노출

2. Program.cs에 Hub 등록
   └── app.MapHub<TestHub>("/testhub")

3. TestClient 프로젝트 생성
   └── dotnet new console
   └── SignalR.Client 패키지 추가

4. HubConnectionService 구현
   └── 연결/재연결 로직
   └── 이벤트 핸들러 등록

5. REPL 루프 구현
   └── 명령어 파싱
   └── 명령어 실행
   └── 결과 출력

6. 개별 명령어 구현
   └── shell → files → read → terminal → copilot
```

---

## 의존성

### MobileAICLI (서버)
- 변경 없음 (기존 서비스 재사용)
- TestHub 추가

### MobileAICLI.TestClient (클라이언트)
```xml
<PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="8.0.0" />
<PackageReference Include="Spectre.Console" Version="0.49.1" />  <!-- 선택: 예쁜 출력 -->
```

---

## 제약 조건

1. **보안**: TestHub는 개발/테스트 용도. 프로덕션에서는 비활성화 고려
2. **인증**: 현재 미구현. 필요시 JWT 또는 API Key 추가
3. **동시성**: 한 번에 하나의 스트리밍 명령만 실행 권장

---

## 예상 소요 시간

| 단계 | 예상 시간 |
|------|-----------|
| TestHub 생성 | 30분 |
| TestClient 기본 구조 | 30분 |
| 명령어 구현 (6개) | 1시간 |
| 테스트 러너 + 스크립트 파서 | 1시간 |
| JSON/Console 리포터 | 30분 |
| 테스트 및 디버깅 | 30분 |
| **총계** | **~4시간** |
