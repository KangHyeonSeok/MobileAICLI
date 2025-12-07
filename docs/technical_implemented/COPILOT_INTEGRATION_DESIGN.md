# Copilot CLI 연동 - 기술 설계 문서

> 📌 이 문서는 Copilot CLI 연동의 **전체 범위와 맥락**을 제공합니다.
> 각 Phase의 상세 설계는 개별 문서를 참조하세요.

---

## 1. 프로젝트 개요

### 목표

GitHub Copilot CLI를 Blazor Server 웹 앱에 통합하여, 모바일 환경에서도 Copilot 에이전트 기능을 사용할 수 있도록 한다.

### 기능 수준

| 레벨 | 기능 | 지원 |
|------|------|------|
| Level 1 | 질문-응답 (읽기 전용) | ✅ |
| Level 2 | + 파일 수정 허용 | ✅ |
| **Level 3** | + Git 작업, PR 생성 | ✅ |

**선택 이유**: Copilot CLI의 모든 에이전트 기능 활용. 도구 승인은 사용자 설정 패널로 제어.

---

## 2. 핵심 기술 결정

### 실행 방식

| 방식 | 설명 | 선택 |
|------|------|------|
| **Programmatic 모드** | `copilot -p "prompt"` | ✅ Phase 1 |
| Interactive 모드 | `copilot` 대화형 세션 | ⏳ Phase 2 |

### 실시간 스트리밍

| 방식 | 선택 |
|------|------|
| **SignalR** | ✅ Blazor 네이티브 |

### 도구 승인 정책

| 프리셋 | 설명 |
|--------|------|
| 안전 | 읽기 전용 |
| **중간** (기본) | 로컬 수정 + Git 커밋 |
| 전체 | 모든 도구 허용 |

사용자가 토글 패널로 세분화 제어 가능.

---

## 3. 아키텍처 개요

```
┌─────────────────────────────────────────────────────────────┐
│                      사용자 브라우저                          │
│  ┌─────────────────────────────────────────────────────┐   │
│  │              Copilot.razor (채팅 UI)                 │   │
│  │              + 도구 설정 패널                         │   │
│  └───────────────────────┬─────────────────────────────┘   │
└──────────────────────────│──────────────────────────────────┘
                           │ SignalR (실시간 통신)
┌──────────────────────────▼──────────────────────────────────┐
│                      Blazor Server                          │
│  ┌─────────────────┐    ┌─────────────────┐                │
│  │  CopilotHub     │───▶│ CopilotService  │                │
│  │  (SignalR Hub)  │    │ (비즈니스 로직)   │                │
│  └─────────────────┘    └────────┬────────┘                │
│                                  │                          │
│                                  ▼                          │
│                         ┌─────────────────┐                │
│                         │  Process.Start  │                │
│                         │  copilot -p "?" │                │
│                         └─────────────────┘                │
└─────────────────────────────────────────────────────────────┘
```

---

## 4. 설계 제약 조건

### 보안

| 항목 | 제약 |
|------|------|
| 작업 디렉토리 | `RepositoryPath` 내부만 |
| 도구 승인 | 사용자 설정 패널로 제어 |
| 프롬프트 검증 | 특수문자 이스케이프 |
| 타임아웃 | 120초 |

### 성능

| 항목 | 목표 |
|------|------|
| 첫 응답 시작 | 5초 이내 |
| 동시 세션 | 최소 3개 |
| 메모리 | 세션당 100MB 이하 |

### 환경 요구사항

| 항목 | 요구사항 |
|------|----------|
| Node.js | v22 이상 |
| Copilot CLI | `npm install -g @github/copilot` |
| 인증 | `GH_TOKEN` 환경변수 |

---

## 5. 구현 로드맵

| Phase | 내용 | 예상 기간 | 문서 |
|-------|------|-----------|------|
| **1.1** | Programmatic 모드 기본 구현 | 3일 | [상세](./PHASE_1_1_PROGRAMMATIC_MODE.md) |
| **1.2** | 도구 설정 패널 UI | 2일 | [상세](./PHASE_1_2_TOOL_SETTINGS_PANEL.md) |
| **1.3** | Level 3 기능 테스트 | 2일 | [상세](./PHASE_1_3_LEVEL3_TESTING.md) |
| **2** | Interactive 모드 (PTY) | 미정 | [상세](./PHASE_2_INTERACTIVE_MODE.md) |

**총 Phase 1 예상 기간: 7일**

---

## 6. Phase 요약

### Phase 1.1: Programmatic 모드 기본 구현

**목표**: `copilot -p "prompt"` 호출 + SignalR 스트리밍 + 기본 채팅 UI

**주요 컴포넌트**:
- CopilotService 수정 (프로세스 스트리밍)
- CopilotHub 신규 (SignalR)
- Copilot.razor 수정 (채팅 UI)

→ [Phase 1.1 상세 문서](./PHASE_1_1_PROGRAMMATIC_MODE.md)

---

### Phase 1.2: 도구 설정 패널 UI

**목표**: 사용자가 도구 권한을 토글로 제어할 수 있는 설정 패널

**주요 컴포넌트**:
- CopilotToolSettings 모델 (설정 저장)
- CopilotSettingsPanel.razor (UI)
- CLI 옵션 문자열 생성 로직

→ [Phase 1.2 상세 문서](./PHASE_1_2_TOOL_SETTINGS_PANEL.md)

---

### Phase 1.3: Level 3 기능 테스트

**목표**: 파일 수정, Git 작업, PR 생성 등 Level 3 기능 종합 검증

**주요 작업**:
- Level 1~3 기능 테스트
- 에러 케이스 검증
- 보안 검증
- 성능 측정
- 사용자 가이드 작성

→ [Phase 1.3 상세 문서](./PHASE_1_3_LEVEL3_TESTING.md)

---

### Phase 2: Interactive 모드 (추후)

**목표**: 대화 컨텍스트 유지 (후속 질문이 이전 대화 참조)

**기술적 과제**:
- PTY (Pseudo-Terminal) 연동
- 응답 경계 감지
- 세션 관리

**상태**: Phase 1 완료 후 필요에 따라 진행

→ [Phase 2 상세 문서](./PHASE_2_INTERACTIVE_MODE.md)

---

## 7. 참고 자료

- [GitHub Copilot CLI 공식 문서](https://docs.github.com/en/copilot/concepts/agents/about-copilot-cli)
- [Copilot CLI npm 패키지](https://www.npmjs.com/package/@github/copilot)
- 기존 서비스: `MobileAICLI/Services/CopilotService.cs`
- 기존 설정: `MobileAICLI/Models/MobileAICLISettings.cs`

---

## 8. 문서 구조

```
docs/technical/
├── COPILOT_INTEGRATION_DESIGN.md      # 👈 이 문서 (전체 개요)
├── PHASE_1_1_PROGRAMMATIC_MODE.md     # Phase 1.1 상세
├── PHASE_1_2_TOOL_SETTINGS_PANEL.md   # Phase 1.2 상세
├── PHASE_1_3_LEVEL3_TESTING.md        # Phase 1.3 상세
└── PHASE_2_INTERACTIVE_MODE.md        # Phase 2 상세
```

---

*이 문서는 설계 방향만 제시합니다. 구체적인 코드 구현은 AI가 각 Phase 설계를 기반으로 생성합니다.*
