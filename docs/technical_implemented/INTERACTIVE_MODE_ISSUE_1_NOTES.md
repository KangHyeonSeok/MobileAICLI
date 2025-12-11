# Interactive Mode - Implementation Notes (Issue 1)

> 📌 Related: [INTERACTIVE_MODE_DETAILED_DESIGN.md](./INTERACTIVE_MODE_DETAILED_DESIGN.md)  
> 📌 Date: 2024-12-11  
> 📌 Issue: #1 - PTY Integration and CopilotInteractiveSession Implementation

---

## 구현 개요

### 완료된 작업

1. **Pty.Net 패키지 통합** (v0.1.16-pre)
   - 최신 안정 버전 사용 (v2.0.0은 존재하지 않음)
   - 크로스 플랫폼 PTY 지원 (Windows/Linux/macOS)

2. **CopilotInteractiveSession 구현**
   - PTY 프로세스 생명주기 관리
   - stdin/stdout 통신
   - 응답 경계 감지 (프롬프트 패턴 + 타임아웃)
   - ANSI 제어 시퀀스 필터링

3. **CopilotSessionService 구현**
   - 세션 풀 관리 (ConcurrentDictionary 기반)
   - 사용자당 1세션 제한
   - 비활성 세션 자동 정리 (15분 타임아웃)
   - 최대 세션 수 제한 (기본 20개)

4. **CopilotInteractiveHub 구현**
   - StartSession: 새 세션 생성 및 초기화
   - SendMessage: 스트리밍 응답 반환
   - EndSession: 세션 정리

5. **단위 테스트** (24개 통과, 1개 스킵)
   - CopilotInteractiveSession 테스트
   - CopilotSessionService 테스트
   - ANSI 필터링 테스트

---

## 기술 세부사항

### 1. Pty.Net API 사용법

#### 버전 차이
설계 문서에서는 v2.0.0을 언급했으나, 실제 NuGet에서 사용 가능한 최신 버전은 **v0.1.16-pre**입니다.

#### API 차이점

| 설계 문서 (가정) | 실제 API (v0.1.16-pre) |
|----------------|----------------------|
| `PtyProvider.SpawnAsync(options, token)` | `PtyProvider.Spawn(command, width, height, workingDirectory, options)` |
| `connection.WriterStream` | `connection.WriteAsync(string)` |
| `connection.ReaderStream` | `connection.PtyData` event |
| `connection.DataReceived` event | `connection.PtyData` event |
| `connection.ProcessExited` event | `connection.PtyDisconnected` event |

#### 실제 사용 예시

```csharp
// PTY 프로세스 시작
_ptyConnection = PtyProvider.Spawn(
    command: "copilot",
    width: 80,
    height: 24,
    workingDirectory: repositoryPath,
    options: new BackendOptions()
);

// 이벤트 구독
_ptyConnection.PtyData += OnDataReceived;
_ptyConnection.PtyDisconnected += OnProcessExited;

// 데이터 쓰기
await _ptyConnection.WriteAsync("prompt\n");
```

### 2. 응답 경계 감지 전략

#### 구현 방식
1. **이벤트 기반 버퍼링**
   - `PtyData` 이벤트로 수신된 데이터를 `StringBuilder`에 축적
   - 별도의 StreamReader 불필요 (이벤트 핸들러가 자동 처리)

2. **프롬프트 패턴 감지**
   - 정규식 패턴: `">\s?$"` (설정 가능)
   - 버퍼에서 패턴 발견 시 응답 완료로 간주

3. **타임아웃 기반 백업**
   - 마지막 출력 후 3초(설정 가능) 경과 시 응답 완료
   - 프롬프트 패턴 감지 실패 시 안전장치 역할

4. **청크 스트리밍**
   - 512자마다 부분 응답 yield
   - 실시간 스트리밍 효과 제공

### 3. ANSI 필터링

#### 지원하는 시퀀스
- **CSI 시퀀스**: `\x1b\[[0-9;]*[A-Za-z]`
  - 색상, 커서 이동, 화면 지우기 등
- **일부 OSC 시퀀스**: `\x1b\][^\x07]*\x07`
  - 제목 설정 등 (완전하지 않음, 알려진 제한 사항)

#### 제한 사항
- OSC 시퀀스 처리가 완벽하지 않음
  - BEL(\x07) 이외의 종료자(ST: \x1b\\) 미지원
  - 향후 개선 가능

### 4. 세션 관리

#### 데이터 구조
```csharp
ConcurrentDictionary<string, SessionMetadata> _sessions
ConcurrentDictionary<string, string> _userToSession  // userId -> sessionId 매핑
```

#### 세션 생명주기
1. **생성**: 사용자당 1개 세션 허용
   - 기존 세션 자동 제거 후 신규 생성
2. **접근**: userId 기반 권한 검증
3. **정리**:
   - 비활성 15분 후 자동 제거
   - 최대 세션 수 초과 시 가장 오래된 세션 제거
   - 백그라운드 타이머(1분마다) 실행

#### 동시성 처리
- `ConcurrentDictionary`로 스레드 안전성 보장
- 세션별 `SemaphoreSlim`으로 읽기/쓰기 직렬화

---

## 알려진 제한 사항

### 1. 테스트 환경 제약
- `copilot` CLI가 설치되지 않은 환경에서 통합 테스트 불가
- PTY 프로세스 실행이 필요한 테스트는 Mock/Stub 사용

### 2. 플랫폼별 차이
- Pty.Net은 크로스 플랫폼이지만 플랫폼별 동작 차이 가능
- Windows: WinPTY 사용
- Linux/macOS: Native PTY 사용

### 3. ANSI 필터링
- OSC 시퀀스 처리 불완전 (테스트 1개 스킵)
- 복잡한 ANSI 시퀀스는 일부 남을 수 있음

---

## 보안 고려사항

### 1. 입력 검증
- 최대 길이 제한: 10,000자 (설정 가능)
- 제어 문자 필터링:
  - EOF (\x04)
  - ETX (\x03)
  - SUB (\x1A)
  - "exit" 명령 차단

### 2. 사용자 격리
- `userId` 기반 세션 접근 제어
- 타인의 세션 접근 차단

### 3. 리소스 제한
- 사용자당 1세션
- 전역 최대 20세션
- 15분 비활성 타임아웃

---

## 성능 특성

### 메모리 사용량
- 세션당 ~50MB (copilot 프로세스)
- 20세션 기준 ~1GB 예상

### CPU 사용량
- 대기 중: 낮음
- 응답 생성 중: copilot 프로세스에 의존

### 네트워크
- SignalR 스트리밍: 청크 단위 전송
- 대역폭: 응답 크기에 비례

---

## 향후 개선 사항

### 1. ANSI 처리 개선
- OSC 시퀀스 완전 지원 (ST 종료자)
- 프론트엔드 ANSI 렌더링 옵션 (xterm.js)

### 2. 테스트 개선
- PTY Mock 구현으로 통합 테스트 가능하게
- 실제 copilot 프로세스 없이 테스트

### 3. 에러 복구
- 프로세스 충돌 시 자동 재시작
- 세션 상태 복원 메커니즘

### 4. 모니터링
- 세션 메트릭 수집
  - 평균 세션 지속 시간
  - 메시지당 응답 시간
  - 에러율

---

## 참고 자료

- [Pty.Net GitHub Issue #38](https://github.com/microsoft/vs-pty.net/issues/38) - API 사용 예시
- [INTERACTIVE_MODE_DETAILED_DESIGN.md](./INTERACTIVE_MODE_DETAILED_DESIGN.md) - 설계 문서
- [Pty.Net NuGet](https://www.nuget.org/packages/Pty.Net/) - 패키지 정보

---

*이 문서는 Issue 1 구현 완료 후 작성되었으며, 실제 구현 내용과 설계 문서의 차이점을 기록합니다.*
