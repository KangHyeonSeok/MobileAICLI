# Phase 1.2: 도구 설정 패널 UI

> 📌 상위 문서: [COPILOT_INTEGRATION_DESIGN.md](./COPILOT_INTEGRATION_DESIGN.md)
> 📌 선행 작업: [Phase 1.1: Programmatic 모드 기본 구현](./PHASE_1_1_PROGRAMMATIC_MODE.md)

---

## 목표

사용자가 Copilot CLI의 도구 권한을 세분화하여 제어할 수 있는 설정 패널 UI를 구현한다.

---

## 범위

| 포함 | 제외 |
|------|------|
| 도구 권한 토글 UI | CLI 기본 기능 (Phase 1.1) |
| 프리셋 (안전/중간/전체) | Level 3 기능 테스트 (Phase 1.3) |
| 설정 저장/로드 | |
| CLI 옵션 문자열 생성 | |

---

## UI 설계

### 설정 패널 와이어프레임

```
┌─────────────────────────────────────────────────────────┐
│  🔧 Copilot 도구 권한 설정                        [X]   │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  ⚠️ 전체 허용 (위험)                                     │
│  ┌────────────────────────────────────────────────────┐ │
│  │ [  ] 모든 도구 허용 (--allow-all-tools)            │ │
│  └────────────────────────────────────────────────────┘ │
│                                                         │
│  📁 파일 작업                                           │
│  ┌────────────────────────────────────────────────────┐ │
│  │ [✓] 파일 읽기 (cat, head, tail)                    │ │
│  │ [✓] 파일 쓰기/수정 (write)                         │ │
│  │ [  ] 파일 삭제 (rm)                     ⚠️ 위험    │ │
│  └────────────────────────────────────────────────────┘ │
│                                                         │
│  🔀 Git 작업                                            │
│  ┌────────────────────────────────────────────────────┐ │
│  │ [✓] 상태 확인 (git status, log, diff)              │ │
│  │ [✓] 브랜치 작업 (git branch, checkout)             │ │
│  │ [✓] 커밋 (git add, commit)                         │ │
│  │ [  ] 푸시 (git push)                    ⚠️ 원격    │ │
│  │ [  ] 강제 작업 (git reset --hard)       ⚠️ 위험    │ │
│  └────────────────────────────────────────────────────┘ │
│                                                         │
│  🌐 GitHub 작업                                         │
│  ┌────────────────────────────────────────────────────┐ │
│  │ [✓] PR/Issue 조회                                  │ │
│  │ [  ] PR 생성/수정                       ⚠️ 원격    │ │
│  │ [  ] Issue 생성/수정                    ⚠️ 원격    │ │
│  └────────────────────────────────────────────────────┘ │
│                                                         │
│  💻 쉘 명령                                             │
│  ┌────────────────────────────────────────────────────┐ │
│  │ [✓] 기본 명령 (ls, pwd, echo)                      │ │
│  │ [  ] 패키지 설치 (npm, pip)             ⚠️ 주의    │ │
│  │ [  ] 시스템 명령                        ⚠️ 위험    │ │
│  └────────────────────────────────────────────────────┘ │
│                                                         │
│  ┌─────────────────────────────────────────────────────┐│
│  │ [프리셋: 안전] [프리셋: 중간] [프리셋: 전체]        ││
│  └─────────────────────────────────────────────────────┘│
│                                                         │
│                                  [저장] [취소] [초기화] │
└─────────────────────────────────────────────────────────┘
```

### 접근 방법

- Copilot.razor 상단에 ⚙️ 설정 버튼 배치
- 클릭 시 모달 또는 슬라이드 패널로 표시
- 설정 변경 시 즉시 다음 프롬프트에 적용

---

## 도구 매핑 테이블

| UI 토글 | CLI 옵션 | 기본값 |
|---------|----------|--------|
| 모든 도구 허용 | `--allow-all-tools` | ❌ |
| 파일 읽기 | `--allow-tool 'shell(cat,head,tail,less)'` | ✅ |
| 파일 쓰기 | `--allow-tool 'write'` | ✅ |
| 파일 삭제 | `--allow-tool 'shell(rm)'` | ❌ |
| Git 상태 | `--allow-tool 'shell(git status,git log,git diff)'` | ✅ |
| Git 브랜치 | `--allow-tool 'shell(git branch,git checkout)'` | ✅ |
| Git 커밋 | `--allow-tool 'shell(git add,git commit)'` | ✅ |
| Git 푸시 | `--allow-tool 'shell(git push)'` | ❌ |
| Git 강제 | `--allow-tool 'shell(git reset)'` | ❌ |
| PR/Issue 조회 | `--allow-tool 'shell(gh pr list,gh issue list)'` | ✅ |
| PR 생성 | `--allow-tool 'shell(gh pr create)'` | ❌ |
| Issue 생성 | `--allow-tool 'shell(gh issue create)'` | ❌ |
| 기본 쉘 | `--allow-tool 'shell(ls,pwd,echo,find)'` | ✅ |
| 패키지 설치 | `--allow-tool 'shell(npm,pip,yarn)'` | ❌ |
| 시스템 명령 | `--allow-tool 'shell'` (전체) | ❌ |

---

## 프리셋 정의

### 안전 (Safe)

```
FileRead: true
나머지 모두: false
```

→ 읽기 전용, 어떤 수정도 불가

### 중간 (Medium) - 기본값

```
FileRead: true
FileWrite: true
FileDelete: false
GitStatus: true
GitBranch: true
GitCommit: true
GitPush: false
GitForce: false
PrRead: true
PrWrite: false
IssueWrite: false
ShellBasic: true
ShellPackage: false
ShellSystem: false
AllowAll: false
```

→ 로컬 파일 수정 + Git 커밋까지 (원격 작업 제외)

### 전체 (Full)

```
AllowAll: true
```

→ 모든 도구 허용 (위험 경고 표시)

---

## 컴포넌트 설계

### 1. CopilotToolSettings 모델

**책임**:
- 각 도구의 활성화 상태 저장
- 프리셋 적용
- CLI 옵션 문자열 생성

**속성**:
```
AllowAll: bool
FileRead: bool
FileWrite: bool
FileDelete: bool
GitStatus: bool
GitBranch: bool
GitCommit: bool
GitPush: bool
GitForce: bool
PrRead: bool
PrWrite: bool
IssueWrite: bool
ShellBasic: bool
ShellPackage: bool
ShellSystem: bool
```

**메서드**:
```
ApplyPreset(preset: "safe" | "medium" | "full")
BuildCliOptions() → string  // "--allow-tool 'X' --deny-tool 'Y'" 생성
```

### 2. CopilotSettingsPanel.razor 신규 컴포넌트

**책임**:
- 설정 패널 UI 렌더링
- 토글 상태 관리
- 프리셋 버튼 처리

**이벤트**:
- `OnSave(CopilotToolSettings)` - 저장 시 부모에게 전달
- `OnCancel()` - 취소

### 3. Copilot.razor 수정

- ⚙️ 설정 버튼 추가
- 설정 패널 모달 표시/숨김
- CopilotToolSettings 상태 관리
- 프롬프트 전송 시 CLI 옵션 포함

---

## 설정 저장

### 방법 1: 브라우저 LocalStorage (권장)

- 사용자별 설정 유지
- 서버 재시작해도 유지
- Blazor `IJSRuntime`으로 접근

### 방법 2: 서버 설정 파일

- `appsettings.json`의 기본값으로 사용
- UI에서 오버라이드

---

## 위험 경고 UX

| 수준 | 색상 | 아이콘 | 대상 |
|------|------|--------|------|
| 안전 | 없음 | 없음 | 파일 읽기, Git 상태 |
| 주의 | 노랑 | ⚠️ | 패키지 설치 |
| 원격 | 주황 | ⚠️ 원격 | Git 푸시, PR/Issue 생성 |
| 위험 | 빨강 | ⚠️ 위험 | 파일 삭제, Git 강제, 시스템 명령 |

"모든 도구 허용" 선택 시:
```
┌──────────────────────────────────────────────────┐
│  ⚠️ 경고                                         │
│                                                  │
│  모든 도구를 허용하면 Copilot이 파일 삭제,       │
│  시스템 명령 실행 등을 승인 없이 수행할 수       │
│  있습니다.                                       │
│                                                  │
│  신뢰할 수 있는 환경에서만 사용하세요.           │
│                                                  │
│            [취소]  [위험을 감수하고 허용]        │
└──────────────────────────────────────────────────┘
```

---

## 테스트 시나리오

- [ ] 프리셋 버튼 클릭 시 토글 상태 일괄 변경
- [ ] 개별 토글 변경 후 저장
- [ ] 설정 저장 후 페이지 새로고침해도 유지
- [ ] "모든 도구 허용" 선택 시 경고 표시
- [ ] 설정에 따라 CLI 옵션 문자열 올바르게 생성
- [ ] 초기화 버튼으로 기본값 복원

---

## 예상 기간

**2일**

| 일차 | 작업 |
|------|------|
| 1일차 | CopilotToolSettings 모델 + CopilotSettingsPanel UI |
| 2일차 | LocalStorage 저장 + Copilot.razor 통합 + 테스트 |

---

## 다음 단계

→ [Phase 1.3: Level 3 기능 테스트](./PHASE_1_3_LEVEL3_TESTING.md)
