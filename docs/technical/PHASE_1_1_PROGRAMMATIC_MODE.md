# Phase 1.1: Programmatic 모드 기본 구현

> 📌 상위 문서: [COPILOT_INTEGRATION_DESIGN.md](./COPILOT_INTEGRATION_DESIGN.md)

---

## 목표

Copilot CLI의 Programmatic 모드(`copilot -p "prompt"`)를 사용하여 기본 질문-응답 기능을 구현한다.

---

## 단계별 접근

**먼저 간단한 쉘 명령으로 구조를 검증한 후, Copilot CLI를 통합한다.**

| 단계 | 내용 | 목표 |
|------|------|------|
| **1.1.1** | Shell 명령 실행 + 스트리밍 | 기본 구조 검증 |
| **1.1.2** | Copilot CLI 통합 | 실제 기능 구현 |

---

## Phase 1.1.1: Shell 명령 실행 기반 구조 검증

### 목표

간단한 쉘 명령(`ping`, `ls`, `echo` 등)을 실행하고 결과를 SignalR로 스트리밍하여 **기본 아키텍처를 검증**한다.

### 왜 먼저 하는가?

| 이유 | 설명 |
|------|------|
| 복잡도 분리 | 프로세스 실행 + SignalR을 먼저 검증 |
| 빠른 피드백 | Copilot 인증 없이 즉시 테스트 가능 |
| 디버깅 용이 | 문제 발생 시 원인 파악 쉬움 |

### 구현 범위

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  TestShell.razor│────▶│  ShellHub       │────▶│ ShellService    │
│  (테스트 UI)    │◀────│  (SignalR)      │◀────│ (Process 실행)  │
└─────────────────┘     └─────────────────┘     └────────┬────────┘
                                                         │
                                                         ▼
                                                ┌─────────────────┐
                                                │  ping/ls/echo   │
                                                │  (쉘 명령)       │
                                                └─────────────────┘
```

### 컴포넌트

#### 1. ShellStreamingService (신규)

**책임**:
- 쉘 명령 프로세스 생성
- stdout/stderr 비동기 스트리밍
- 타임아웃 처리

**제약**:
- 허용된 명령만 실행 (화이트리스트)
- `RepositoryPath` 내에서만 실행

#### 2. ShellHub (신규)

**책임**:
- SignalR Hub
- 클라이언트-서버 실시간 통신

**메서드**:
| 메서드 | 방향 | 설명 |
|--------|------|------|
| `ExecuteCommand(command)` | Client → Server | 명령 실행 요청 |
| `ReceiveOutput(text)` | Server → Client | 출력 스트리밍 |
| `ReceiveError(text)` | Server → Client | 에러 출력 |
| `ReceiveComplete(exitCode)` | Server → Client | 실행 완료 |

#### 3. TestShell.razor (신규, 테스트용 페이지)

**UI**:
- 명령어 입력창
- 실행 버튼
- 출력 영역 (실시간 업데이트)
- 상태 표시 (실행 중/완료/에러)

### 테스트 시나리오

```bash
# 테스트할 명령들 (macOS/Linux)
echo "Hello World"           # 단순 출력
ping -c 3 localhost          # 스트리밍 출력
ls -la                       # 디렉토리 목록
cat README.md                # 파일 내용

# Windows
ping -n 3 localhost
dir
type README.md
```

### 검증 항목

- [ ] 명령 실행 후 결과 수신
- [ ] 출력이 실시간으로 스트리밍되는가
- [ ] stderr도 정상 수신되는가
- [ ] 프로세스 종료 후 exitCode 수신
- [ ] 타임아웃 동작 확인 (30초)
- [ ] 클라이언트 연결 끊김 시 프로세스 정리

### 예상 기간: 1일

---

## Phase 1.1.2: Copilot CLI 통합

### 목표

검증된 구조 위에 Copilot CLI를 통합한다.

### 변경 사항

| 컴포넌트 | Phase 1.1.1 | Phase 1.1.2 |
|----------|-------------|-------------|
| Service | ShellStreamingService | **CopilotService** (확장/재사용) |
| Hub | ShellHub | **CopilotHub** (확장) |
| UI | TestShell.razor | **Copilot.razor** (채팅 UI) |
| 명령 | `ping`, `ls` | `copilot -p "prompt"` |

### 추가 구현

- 프롬프트 이스케이프 처리
- Copilot CLI 설치 확인
- 인증 상태 확인
- 채팅 형태 UI (메시지 히스토리)
- 사용자/Copilot 메시지 구분

### 예상 기간: 2일

---

## 전체 아키텍처 (Phase 1.1 완료 후)

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  Copilot.razor  │────▶│  CopilotHub     │────▶│ CopilotService  │
│  (채팅 UI)      │◀────│  (SignalR)      │◀────│ (Process 관리)  │
└─────────────────┘     └─────────────────┘     └────────┬────────┘
                                                         │
                                                         ▼
                                                ┌─────────────────┐
                                                │ copilot -p "?"  │
                                                │ (CLI 프로세스)   │
                                                └─────────────────┘
```

---

## 데이터 흐름 (최종)

```
1. 사용자가 입력창에 질문 작성 후 전송
   │
   ▼
2. Copilot.razor → SignalR → CopilotHub.SendPrompt(prompt)
   │
   ▼
3. CopilotHub → CopilotService.SendPromptStreamingAsync()
   │
   ▼
4. CopilotService: Process.Start("copilot", "-p \"prompt\"")
   │
   ▼
5. stdout 라인 읽기 → CopilotHub.ReceiveOutput() → 클라이언트
   │ (반복)
   ▼
6. 프로세스 종료 → CopilotHub.ReceiveComplete() → UI 업데이트
```

---

## 설정 구조

`appsettings.json`에 추가:

```json
{
  "MobileAICLI": {
    "Copilot": {
      "ExecutablePath": "copilot",
      "TimeoutSeconds": 120,
      "MaxConcurrentSessions": 3
    }
  }
}
```

---

## 에러 처리

| 에러 | 감지 방법 | 처리 |
|------|----------|------|
| CLI 미설치 | Process 시작 실패 | 설치 안내 메시지 |
| 인증 실패 | stderr 메시지 파싱 | 로그인 안내 |
| 타임아웃 | CancellationToken | "시간 초과" 표시 + 프로세스 Kill |
| 프로세스 충돌 | ExitCode != 0 | 에러 메시지 표시 |

---

## 환경 요구사항 (Phase 1.1.2)

| 항목 | 요구사항 |
|------|----------|
| Node.js | v22 이상 |
| Copilot CLI | `npm install -g @github/copilot` |
| 인증 | `GH_TOKEN` 환경변수 또는 `copilot` 로그인 완료 |
| Trusted Folder | `~/.copilot/config.json`에 작업 디렉토리 등록 |

---

## 전체 테스트 시나리오 (Phase 1.1 완료 시)

- [ ] 기본 질문에 대한 응답 수신
- [ ] 응답이 실시간으로 스트리밍되는지 확인
- [ ] 긴 응답 (100줄 이상) 처리
- [ ] 타임아웃 발생 시 정상 종료
- [ ] 클라이언트 연결 끊김 시 프로세스 정리
- [ ] 특수문자 포함 프롬프트 (`"`, `'`, `\n` 등)
- [ ] 동시 세션 (최소 3개)

---

## 예상 기간

**총 3일**

| 단계 | 작업 | 기간 |
|------|------|------|
| **1.1.1** | Shell 명령 실행 + SignalR 스트리밍 구조 검증 | 1일 |
| **1.1.2** | Copilot CLI 통합 + 채팅 UI | 2일 |

---

## 다음 단계

→ [Phase 1.2: 도구 설정 패널 UI](./PHASE_1_2_TOOL_SETTINGS_PANEL.md)
