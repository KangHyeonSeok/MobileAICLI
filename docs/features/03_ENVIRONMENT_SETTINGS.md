# 환경 설정 관리 기능

## 개요
MobileAICLI의 주요 환경 설정을 웹 UI에서 동적으로 관리할 수 있는 기능입니다.

## 주요 기능

### 1. 경로 설정
- **Repository Path**: 파일 작업의 기본 경로
- **GitHub Copilot Command**: Copilot CLI 실행 명령어
- **GitHub CLI Path**: gh 실행 파일 경로
- **Git CLI Path**: git 실행 파일 경로 (기본값: git)

### 2. 허용 명령어 관리
- 실행 가능한 shell 명령어를 화이트리스트로 관리
- 동적으로 추가/제거 가능
- 위험한 명령어 자동 차단 (rm -rf, dd, mkfs 등)

### 3. 허용 작업 루트
- 파일 작업이 허용되는 디렉토리 목록
- 와일드카드 패턴 지원 (예: `/path/to/workspace/*`)
- 실시간 경로 유효성 검증

### 4. 비밀번호 변경
- 기존 비밀번호 검증 후 변경
- PBKDF2 해시 알고리즘 사용 (SHA256, 100,000 반복)
- 최소 8자 이상 요구
- 변경 내역 감사 로그 기록

## 사용 방법

1. 메뉴에서 "Settings" 클릭
2. 원하는 설정 항목 수정
3. "Save Settings" 버튼 클릭
4. 일부 설정은 앱 재시작 필요 (안내 메시지 표시)

## 보안 기능

- 모든 설정 변경 내역이 감사 로그에 기록됨
- 민감 정보는 로그에서 자동으로 마스킹
- 비밀번호는 안전한 해시 알고리즘으로 저장
- 위험한 명령어 및 경로 입력 차단

## 기술적 특징

- SignalR을 통한 실시간 통신
- `IOptionsSnapshot`으로 런타임 설정 리로딩
- appsettings.json에 영구 저장
- 포괄적인 입력 유효성 검사
- 단위 테스트 완비

## 관련 파일

- UI: `Components/Pages/Settings.razor`
- 서비스: `Services/SettingsService.cs`
- Hub: `Hubs/SettingsHub.cs`
- 모델: `Models/MobileAICLISettings.cs`
- 감사 로그: `Services/AuditLogService.cs`
