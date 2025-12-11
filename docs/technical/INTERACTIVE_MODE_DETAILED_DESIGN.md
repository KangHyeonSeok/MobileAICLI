# Interactive 모드 상세 설계

> 📌 상위 문서: [PHASE_2_INTERACTIVE_MODE.md](./PHASE_2_INTERACTIVE_MODE.md)
> 📌 작성일: 2024-12-11

---

## 1. 개요

### 목적
Copilot CLI의 Interactive 모드를 통해 대화 컨텍스트를 유지하고, 후속 질문이 이전 대화를 참조할 수 있는 채팅 환경을 제공한다.

### 기존 Programmatic 모드와의 차이점

| 항목 | Programmatic (현재) | Interactive (신규) |
|------|---------------------|-------------------|
| 페이지 | `Copilot.razor` | `CopilotInteractive.razor` (신규) |
| 명령 실행 | `copilot -p "prompt"` 매번 | `copilot` 프로세스 유지 |
| 컨텍스트 | 매 요청 독립적 | 세션 내 유지 |
| 프로세스 | 요청당 생성/종료 | 세션 시작 시 생성, 종료 시 파괴 |
| 적합한 사용 | 일회성 질문 | 연속적인 대화 |

---

## 2. 아키텍처

### 2.1 전체 구조

```
┌────────────────────────────────────────────────────────────┐
│                        Blazor UI                            │
├────────────────────────────────────────────────────────────┤
│  Copilot.razor              CopilotInteractive.razor        │
│  (Programmatic Mode)        (Interactive Mode - 신규)       │
│         │                           │                       │
│         ▼                           ▼                       │
├─────────┴───────────────────────────┴───────────────────────┤
│              SignalR Hubs                                   │
├────────────────────────────────────────────────────────────┤
│  CopilotHub                  CopilotInteractiveHub (신규)   │
│         │                           │                       │
│         ▼                           ▼                       │
├─────────┴───────────────────────────┴───────────────────────┤
│              Services                                       │
├────────────────────────────────────────────────────────────┤
│  CopilotStreamingService    CopilotSessionService (신규)    │
│  (매번 프로세스 생성)          │                             │
│                              ▼                              │
│                      CopilotInteractiveSession (신규)       │
│                      (프로세스 생명주기 관리)                 │
└────────────────────────────────────────────────────────────┘
                               │
                               ▼
                    ┌──────────────────┐
                    │  copilot process │
                    │  (PTY/Pty.Net)   │
                    │  stdin/stdout    │
                    └──────────────────┘
```

### 2.2 컴포넌트 역할

| 컴포넌트 | 책임 | 신규/수정 |
|----------|------|----------|
| `CopilotInteractive.razor` | Interactive UI, 채팅 인터페이스 | ✨ 신규 |
| `CopilotInteractiveHub` | SignalR 통신, 세션 관리 | ✨ 신규 |
| `CopilotSessionService` | 세션 풀 관리, 생명주기 제어 | ✨ 신규 |
| `CopilotInteractiveSession` | 단일 세션의 PTY 프로세스 관리 | ✨ 신규 |

---

## 3. 데이터 흐름

### 3.1 세션 시작

```
사용자
  │
  │ 1. 페이지 접속
  ▼
CopilotInteractive.razor
  │
  │ 2. SignalR 연결
  ▼
CopilotInteractiveHub
  │
  │ 3. StartSession() 호출
  ▼
CopilotSessionService
  │
  │ 4. 새 세션 생성 요청
  ▼
CopilotInteractiveSession.Create()
  │
  │ 5. PTY로 copilot 프로세스 시작
  │    FileName: "copilot"
  │    Arguments: (없음 - interactive mode)
  ▼
copilot 프로세스 대기 중
  │
  │ 6. 초기 프롬프트 감지 ("> ")
  ▼
Hub → Client: SessionReady
```

### 3.2 메시지 전송 및 응답

```
사용자
  │
  │ 1. 질문 입력
  ▼
CopilotInteractive.razor
  │
  │ 2. SendMessage(sessionId, prompt)
  ▼
CopilotInteractiveHub
  │
  │ 3. GetSession(sessionId)
  ▼
CopilotSessionService
  │
  │ 4. session.WriteAsync(prompt + "\n")
  ▼
copilot 프로세스 stdin
  │
  │ 5. 처리 중...
  ▼
copilot 프로세스 stdout
  │
  │ 6. 출력 스트림 읽기
  ▼
CopilotInteractiveSession
  │
  │ 7. 응답 버퍼링 및 프롬프트 감지
  │    - 프롬프트 패턴 ("> ") 감지 시 응답 완료
  │    - 타임아웃(3초) 시 응답 완료
  ▼
CopilotInteractiveHub
  │
  │ 8. SignalR 스트리밍
  ▼
Client: ReceiveChunk, ReceiveComplete
```

### 3.3 세션 종료

```
사용자
  │
  │ 1. 페이지 이탈 또는 명시적 종료
  ▼
CopilotInteractive.razor
  │
  │ 2. EndSession(sessionId)
  ▼
CopilotInteractiveHub
  │
  │ 3. sessionService.RemoveSession(sessionId)
  ▼
CopilotSessionService
  │
  │ 4. session.Dispose()
  ▼
CopilotInteractiveSession
  │
  │ 5. "exit\n" → stdin
  │    또는 Process.Kill()
  ▼
copilot 프로세스 종료
```

---

## 4. 핵심 로직 설계

### 4.1 CopilotSessionService

**책임**: 세션 풀 관리, 세션 생명주기 제어

**주요 메서드**:
```
CreateSessionAsync(userId) → sessionId
GetSession(sessionId) → CopilotInteractiveSession
RemoveSession(sessionId)
CleanupInactiveSessions() // 백그라운드 작업
```

**세션 관리 규칙**:
- 사용자당 최대 1개 세션
- 신규 세션 생성 시 기존 세션 자동 종료
- 비활성 세션 타임아웃: 기본 15분 (`MobileAICLISettings.CopilotInteractiveSessionTimeoutMinutes`)
- 최대 세션 수: 기본 20개 (전역, `MobileAICLISettings.CopilotInteractiveMaxSessions`)

**데이터 구조**:
```
ConcurrentDictionary<string, CopilotInteractiveSession> _sessions
ConcurrentDictionary<string, DateTime> _lastActivityTime
```

**사용자 식별 규칙**:
- `userId`는 SignalR `Context.UserIdentifier` 또는 사전에 정의한 Claim(예: `sub` 또는 `nameidentifier`)에서 가져온다.
- 동일 `userId`에 대해서만 세션 조회/종료가 가능하도록 검증한다.

**세션 정리 로직**:
- 백그라운드 작업(1분마다 실행)
- 비활성 시간 15분 초과 세션 제거
- 최대 세션 수 초과 시 가장 오래된 세션 제거

### 4.2 CopilotInteractiveSession

**책임**: 단일 PTY 프로세스 생명주기, stdin/stdout 관리

**주요 메서드**:
```
CreateAsync() → CopilotInteractiveSession
WriteAsync(text) // stdin에 메시지 전송
ReadResponseAsync() → IAsyncEnumerable<string>
Dispose() // 프로세스 정리
```

**응답 경계 감지 전략**:

1. **프롬프트 패턴 감지** (우선):
   ```
   출력 버퍼: "응답 내용\n> "
                          ↑
                    프롬프트 감지 → 응답 완료
   ```

2. **타임아웃 기반** (보조):
   ```
   마지막 출력 이후 N초(기본 3초, 설정 가능) 경과 → 응답 완료
   ```

3. **특수 시퀀스** (예외 처리):
   ```
   에러 메시지 패턴 감지 → 즉시 에러 처리
   ```

**버퍼링 전략**:
- 줄 단위 버퍼링
- 프롬프트 패턴 제거 후 전송 (프롬프트 패턴은 설정값 또는 초기 세션에서 관측한 실제 프롬프트를 기반으로 Regex로 정의)
- ANSI 제어 시퀀스 필터링

### 4.3 CopilotInteractiveHub

**책임**: SignalR 통신, 사용자 ↔ 세션 매핑

**주요 메서드**:
```
StartSession() → sessionId
SendMessage(sessionId, prompt)
EndSession(sessionId)
```

**연결 관리**:
- `OnConnectedAsync()`: 사용자 인증 확인
- `OnDisconnectedAsync()`: 세션 자동 종료

**에러 처리**:
- 세션 없음: 클라이언트에 에러 전송 → 재시작 유도
- 프로세스 종료: 세션 제거 후 클라이언트에 알림
- 타임아웃: 재시도 또는 세션 재생성

---

## 5. PTY 통합 방법

### 5.1 선택한 방식: Pty.Net

**이유**:
- .NET 네이티브 라이브러리
- Windows/Linux/macOS 크로스 플랫폼
- 활발한 유지보수 (최근 업데이트 확인됨)
- Process.Start()와 유사한 API

**설치**:
```xml
<PackageReference Include="Pty.Net" Version="2.0.0" />
```

### 5.2 PTY 프로세스 생성 패턴

**일반 Process vs PTY 비교**:

| 항목 | Process (현재) | PTY (Interactive) |
|------|----------------|-------------------|
| 표준 입출력 | 파이프 | 가상 터미널 |
| 제어 시퀀스 | 필터링됨 | 그대로 전달 |
| Interactive 앱 | 제한적 | 완전 지원 |
| 프롬프트 감지 | 불가능 | 가능 |

**PTY 초기화 로직**:
```
PtyOptions:
  - Name: "copilot"
  - Arguments: (빈 배열 - interactive mode)
  - WorkingDirectory: RepositoryPath
  - Environment: PATH 등 환경변수
  - Cols: 80, Rows: 24 (기본 터미널 크기)

생성 후:
  1. stdout 읽기 시작 (백그라운드 태스크)
  2. 초기 프롬프트("> ") 대기
  3. 준비 완료 신호 전송
```

### 5.3 ANSI 제어 시퀀스 처리

**필터링이 필요한 이유**:
- Interactive 터미널은 색상, 커서 이동 등 제어 시퀀스 포함
- 웹 UI에는 순수 텍스트만 필요

**처리 방법**:
```
정규식 패턴:
  \x1b\[[0-9;]*[A-Za-z]  // CSI 시퀀스
  \x1b\][^\x07]*\x07     // OSC 시퀀스

적용:
  var cleanText = AnsiEscapeCodePattern.Replace(rawOutput, "");
```

**대안**: ANSI 그대로 유지 + 프론트엔드에서 렌더링
- 장점: 색상 표현 가능
- 단점: 프론트엔드 라이브러리 필요 (xterm.js 등)
- 결정: Phase 2에서는 제거, 향후 고려

---

## 6. UI 설계

### 6.1 CopilotInteractive.razor 구조

**레이아웃**:
```
┌─────────────────────────────────────────┐
│  🤖 Copilot Interactive Chat             │ ← 헤더
├─────────────────────────────────────────┤
│                                          │
│  ┌────────────────────────────────┐    │
│  │ AI: 안녕하세요. 무엇을 도와...  │    │ ← 메시지 영역
│  └────────────────────────────────┘    │   (스크롤 가능)
│                                          │
│  ┌────────────────────────────────┐    │
│  │ User: 이 파일 설명해줘          │    │
│  └────────────────────────────────┘    │
│                                          │
│  ┌────────────────────────────────┐    │
│  │ AI: [응답 스트리밍 중...]       │    │
│  └────────────────────────────────┘    │
│                                          │
├─────────────────────────────────────────┤
│  ┌─────────────────────┐  [전송]  [종료] │ ← 입력 영역
│  │ 질문을 입력하세요... │                │
│  └─────────────────────┘                │
└─────────────────────────────────────────┘
```

**기능 요소**:
- 자동 스크롤 (새 메시지 시)
- 응답 스트리밍 표시 (타이핑 효과)
- 세션 상태 표시 (연결됨/연결 중/오류)
- 대화 히스토리 유지 (클라이언트 측)
- 모바일 환경 기준:
  - 입력 영역은 화면 하단에 고정하고, 소프트 키보드가 올라올 때에도 메시지 영역이 정상적으로 리사이즈되도록 한다.
  - 메시지 영역은 `flex-grow` 기반으로 남은 공간을 차지하도록 구성한다.

### 6.2 기존 페이지와의 차별점

| 기능 | Copilot.razor | CopilotInteractive.razor |
|------|---------------|--------------------------|
| 인터페이스 | 단일 입력/출력 | 채팅 히스토리 |
| 컨텍스트 | 없음 | 세션 내 유지 |
| 사용 패턴 | 일회성 질문 | 연속 대화 |
| 적합한 경우 | 빠른 검색/질문 | 심층 탐구 |

### 6.3 네비게이션 구조

**사이드바 메뉴 추가**:
```
📁 Files
🤖 Copilot           ← 기존 (Programmatic)
💬 Interactive Chat  ← 신규
🖥️ Terminal
⚙️ Settings
```

---

## 7. 에러 처리 및 복구

### 7.1 예상 에러 시나리오

| 에러 상황 | 원인 | 대응 방법 |
|----------|------|----------|
| 프로세스 시작 실패 | copilot 미설치 | 설치 안내 메시지 |
| 프로세스 충돌 | copilot 버그/메모리 | 세션 재생성 제안 |
| 응답 타임아웃 | 너무 긴 응답 | 부분 응답 표시 + 계속 대기 옵션 |
| 세션 없음 | 타임아웃 또는 서버 재시작 | 자동 재연결 |
| PTY 통신 실패 | 플랫폼 문제 | Fallback to Programmatic |

### 7.2 복구 전략

**자동 복구**:
1. 세션 만료 → 클라이언트에서 자동 재시작
2. 프로세스 충돌 → 새 프로세스로 세션 재생성
3. 일시적 통신 오류 → 재시도 3회

**사용자 개입**:
1. 지속적 실패 → "Programmatic 모드 사용" 제안
2. 설치 문제 → 설정 페이지로 이동 유도
3. Interactive 모드에서 중대한 오류 발생 시, UI에서 명확한 배너 메시지와 함께 Programmatic 모드로 이동할 수 있는 버튼을 제공한다.

### 7.3 로깅 및 모니터링

**로그 수준**:
```
INFO:  세션 생성/종료, 메시지 전송
DEBUG: PTY 출력 상세, 프롬프트 감지
ERROR: 프로세스 실패, 타임아웃
```

**메트릭**:
- 활성 세션 수
- 평균 세션 지속 시간
- 메시지당 평균 응답 시간
- 에러율 (세션당)

---

## 8. 성능 및 리소스 관리

### 8.1 리소스 제약

**세션당 리소스**:
- 메모리: ~50MB (copilot 프로세스)
- CPU: 낮음 (대기 상태), 높음 (응답 생성 중)
- 파일 디스크립터: 3개 (stdin/stdout/stderr)

**서버 제약**:
- 최대 동시 세션: 기본 20개 (`MobileAICLISettings.CopilotInteractiveMaxSessions`)
- 예상 메모리 사용량: ~1GB (20 세션 기준)
- 세션당 타임아웃: 기본 15분 (`MobileAICLISettings.CopilotInteractiveSessionTimeoutMinutes`)

### 8.2 최적화 전략

**지연 로딩**:
- 세션은 사용자 요청 시에만 생성
- 프로세스 미리 생성하지 않음

**리소스 회수**:
- 비활성 세션 자동 정리 (백그라운드)
- 메모리 압박 시 오래된 세션 우선 제거

**응답 버퍼 관리**:
- 최대 버퍼 크기: 10KB
- 초과 시 청크 단위 전송

---

## 9. 보안 고려사항

### 9.1 인증 및 권한

**세션 접근 제어**:
- 세션 ID는 사용자별로 격리
- 타인의 세션 접근 차단 (UserId 검증)
  - 서버 로그에는 전체 프롬프트를 남기지 않고, 길이를 제한하거나 앞부분만 남기는 방식으로 Truncate하여 개인정보 노출을 방지한다.

**명령 실행 제약**:
- copilot 프로세스 외 다른 명령 실행 불가
- WorkingDirectory는 RepositoryPath로 제한

### 9.2 입력 검증

**사용자 입력**:
- 최대 길이: 10,000자
- 특수 제어 문자 필터링 (stdin 주입 방지)

**프롬프트 인젝션 방지**:
- 입력에서 "exit", "\x04" (EOF) 등 제어 명령 필터링
- 멀티라인 입력 시 이스케이핑

---

## 10. 테스트 전략

### 10.1 단위 테스트

**테스트 대상**:
- `CopilotSessionService`: 세션 생성/제거/타임아웃
- `CopilotInteractiveSession`: 응답 경계 감지 로직
- ANSI 필터링 정규식

**Mocking**:
- PTY 프로세스 → 테스트용 MockPtyProcess
- 시간 기반 로직 → ISystemClock 인터페이스

### 10.2 통합 테스트

**시나리오**:
1. 세션 시작 → 메시지 전송 → 응답 수신 → 세션 종료
2. 연속 메시지 전송 (컨텍스트 유지 확인)
3. 세션 타임아웃 후 재연결
4. 동시 다중 세션 처리

**테스트 환경**:
- copilot CLI 설치 필수
- 실제 PTY 프로세스 실행

### 10.3 수동 테스트 체크리스트

- [ ] 세션 시작 후 첫 질문 응답
- [ ] 후속 질문이 이전 답변 참조하는지 확인
- [ ] 긴 응답 스트리밍 정상 동작
- [ ] 페이지 이탈 시 세션 정리
- [ ] 동시 여러 사용자 사용
- [ ] 네트워크 지연 환경 테스트
- [ ] 프로세스 충돌 복구

---

## 11. 구현 순서

### Phase 1: 기본 프레임워크 (3일)
1. Pty.Net 패키지 설치 및 POC
2. `CopilotInteractiveSession` 구현
   - PTY 프로세스 생성
   - stdin/stdout 기본 통신
3. 응답 경계 감지 로직 구현
4. 단위 테스트 작성

### Phase 2: 세션 관리 (2일)
5. `CopilotSessionService` 구현
   - 세션 풀 관리
   - 타임아웃 처리
6. `CopilotInteractiveHub` 구현
   - SignalR 통신
   - 세션 생명주기 연동
7. 통합 테스트 작성

### Phase 3: UI 구현 (2일)
8. `CopilotInteractive.razor` 페이지 생성
   - 채팅 인터페이스
   - SignalR 클라이언트 연동
9. 사이드바 메뉴 추가
10. CSS 스타일링

### Phase 4: 안정화 (2일)
11. 에러 처리 강화
12. ANSI 필터링 개선
13. 수동 테스트 및 버그 수정
14. 문서화 (사용자 가이드)

**총 예상 기간: ~9일**

---

## 12. 향후 확장 가능성

### 12.1 기능 확장

- **대화 히스토리 저장**: DB에 세션 히스토리 저장
- **세션 복원**: 서버 재시작 후 세션 복구
- **멀티모달**: 이미지/파일 첨부 지원
- **ANSI 렌더링**: xterm.js로 색상 표현

### 12.2 성능 개선

- **세션 풀링**: 미리 생성된 프로세스 풀
- **응답 캐싱**: 동일 질문 캐싱
- **스트리밍 최적화**: 워드 단위 스트리밍

### 12.3 플랫폼 지원

- **Docker 환경**: PTY 설정 최적화
- **클라우드 배포**: 세션 분산 관리
- **모바일 최적화**: UI 반응형 개선

---

## 13. 의사결정 기록

### 13.1 왜 별도 페이지인가?

**이유**:
- 기존 Programmatic 모드와 완전히 다른 UX (채팅 vs 단일 질문)
- 세션 관리 복잡도 → 독립적 구조가 유지보수 용이
- 사용자 선택권 제공 (빠른 질문 vs 심층 대화)

**대안**:
- 같은 페이지에서 모드 전환 → 상태 관리 복잡, 혼란 유발

### 13.2 왜 Pty.Net인가?

**이유**:
- .NET 네이티브 → 추가 런타임 불필요
- 크로스 플랫폼 → Windows/Linux 모두 지원
- 활발한 커뮤니티 → 버그 수정 빠름

**대안**:
- Node.js 중간 계층 (node-pty) → 과도한 복잡도
- 직접 PTY 구현 → 개발 시간 과다

### 13.3 왜 사용자당 1세션인가?

**이유**:
- 리소스 제약 (서버당 20세션)
- 사용자 혼란 방지 (어느 세션에 질문?)
- 간단한 구현

**대안**:
- 사용자당 N세션 → 리소스 관리 복잡도 증가
- 탭별 세션 → 메모리 부족 위험

---

## 14. 참고 자료

### 14.1 기술 문서

- [Pty.Net GitHub](https://github.com/gui-cs/Pty.Net)
- [Copilot CLI 문서](https://githubnext.com/projects/copilot-cli)
- [SignalR Streaming](https://learn.microsoft.com/en-us/aspnet/core/signalr/streaming)

### 14.2 관련 프로젝트 문서

- [PHASE_2_INTERACTIVE_MODE.md](./PHASE_2_INTERACTIVE_MODE.md) - 상위 계획 문서
- [COPILOT_INTEGRATION_DESIGN.md](./COPILOT_INTEGRATION_DESIGN.md) - 전체 아키텍처
- [PHASE_1_1_PROGRAMMATIC_MODE.md](./PHASE_1_1_PROGRAMMATIC_MODE.md) - 기존 구현

---

*이 문서는 구현 전 설계 검토용이며, 실제 구현 시 세부사항은 조정될 수 있습니다.*
*코드 레벨 구현은 AI가 이 설계를 참조하여 생성합니다.*
