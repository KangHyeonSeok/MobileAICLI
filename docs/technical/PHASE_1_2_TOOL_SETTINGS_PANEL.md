# Phase 1.2: Copilot 설정 패널 UI

> 📌 상위 문서: [COPILOT_INTEGRATION_DESIGN.md](./COPILOT_INTEGRATION_DESIGN.md)
> 📌 선행 작업: [Phase 1.1: Programmatic 모드 기본 구현](./PHASE_1_1_PROGRAMMATIC_MODE.md)

---

## 목표

사용자가 Copilot CLI의 도구 권한뿐 아니라 Copilot/GitHub 인증 토큰(GITHUB_TOKEN) 사용 방식을 직접 제어할 수 있는 설정 패널 UI를 구현한다.

---

## 범위

| 포함 | 제외 |
|------|------|
| 도구 권한 토글 UI | CLI 기본 기능 (Phase 1.1) |
| 프리셋 (안전/중간/전체) | Level 3 기능 테스트 (Phase 1.3) |
| GITHUB_TOKEN 인증 방식 선택 | |
| 설정 저장/로드 | |
| CLI 옵션 문자열 생성 | |

---


## UI 설계

### 설정 패널 와이어프레임

```
┌─────────────────────────────────────────────────────────┐
│  🔧 Copilot 설정 패널                             [X]   │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  🛡️ GITHUB_TOKEN 인증 방식                              │
│  ┌────────────────────────────────────────────────────┐ │
│  │ ( ) 시스템 값 사용 (기본)                          │ │
│  │ ( ) 직접 입력 (아래 입력란 사용)                   │ │
│  │ ( ) 무시 (토큰 없이 실행)                          │ │
│  └────────────────────────────────────────────────────┘ │
│  │ [____________________________]  # 직접 입력 시 토큰 │ │
│                                                         │
│  ⚠️ 전체 허용 (위험)                                     │
│  ┌────────────────────────────────────────────────────┐ │
│  │ [  ] 모든 도구 허용 (--allow-all-tools)            │ │
│  └────────────────────────────────────────────────────┘ │
│                                                         │
│  (이하 기존 도구 권한 UI 동일)
│                                                         │
│                                  [저장] [취소] [초기화] │
└─────────────────────────────────────────────────────────┘
```

### 접근 방법

- Copilot.razor 상단에 ⚙️ 설정 버튼 배치
- 클릭 시 모달 또는 슬라이드 패널로 표시
- 설정 변경 시 즉시 다음 프롬프트에 적용
- GITHUB_TOKEN 인증 방식은 라디오 버튼으로 선택, 직접 입력 시 입력란 활성화
- 저장 시 선택된 방식에 따라 Copilot CLI 실행 환경에 토큰을 주입/무시/입력값 사용

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

### 1. CopilotSettings 모델

**책임**:
- 각 도구의 활성화 상태 저장
- GITHUB_TOKEN 인증 방식 및 값 저장
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

GithubTokenMode: "system" | "custom" | "none"
GithubTokenValue: string
```

**메서드**:
```
ApplyPreset(preset: "safe" | "medium" | "full")
BuildCliOptions() → string  // "--allow-tool 'X' --deny-tool 'Y'" 생성
GetGithubTokenEnv() → (key: string, value: string | null) // 환경 변수 주입 방식 결정
```

### 2. CopilotSettingsPanel.razor 신규 컴포넌트

**책임**:
- 설정 패널 UI 렌더링
- 토글 상태 관리
- GITHUB_TOKEN 인증 방식 선택/입력
- 프리셋 버튼 처리

**이벤트**:
- `OnSave(CopilotSettings)` - 저장 시 부모에게 전달
- `OnCancel()` - 취소

### 3. Copilot.razor 수정

- ⚙️ 설정 버튼 추가
- 설정 패널 모달 표시/숨김
- CopilotSettings 상태 관리
- 프롬프트 전송 시 CLI 옵션 및 인증 토큰 환경 변수 포함

---

## 작업 디렉토리 설정

### 요구사항

1. **UI에서 작업 디렉토리 설정 가능**
   - 설정 패널에 디렉토리 경로 입력 필드 추가
   - 디렉토리 선택 버튼 (가능한 경우)

2. **기본값 설정**
   - 설정된 값이 없으면 OS별 Documents 디렉토리 사용
     - Windows: `%USERPROFILE%\Documents`
     - macOS: `~/Documents`
     - Linux: `~/Documents`

3. **설정 지속성**
   - LocalStorage에 저장하여 브라우저 새로고침 후에도 유지
   - 서버 재시작 시에도 클라이언트 설정 유지

4. **검증**
   - 경로 존재 여부 확인
   - 읽기/쓰기 권한 확인
   - 유효하지 않은 경로 시 경고 표시

### UI 추가 (설정 패널)

```
┌─────────────────────────────────────────────────────────┐
│  📁 작업 디렉토리                                        │
│  ┌────────────────────────────────────────────────────┐ │
│  │ [____________________________] [찾아보기]          │ │
│  │                                                    │ │
│  │ 현재: ~/Documents (기본값)                         │ │
│  │ ✓ 디렉토리 접근 가능                               │ │
│  └────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
```

### 로직 흐름

```
1. 앱 시작
   ↓
2. LocalStorage에서 저장된 경로 확인
   ↓
3-a. 저장된 경로 있음 → 해당 경로 사용
3-b. 저장된 경로 없음 → OS별 Documents 경로 사용
   ↓
4. 경로 유효성 검증
   ↓
5-a. 유효함 → 정상 사용
5-b. 유효하지 않음 → 경고 표시 + 설정 패널 열기 유도
```

---

## 설정 저장

### 방법 1: 브라우저 LocalStorage (권장)

- 사용자별 설정 유지
- 서버 재시작해도 유지
- Blazor `IJSRuntime`으로 접근
- **저장 항목**: 도구 권한 설정, 작업 디렉토리 경로

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
