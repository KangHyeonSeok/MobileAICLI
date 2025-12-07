# UI 개선 개발 계획

## 1. 개요

MobileAICLI의 사용자 경험을 개선하기 위한 UI 개선 작업입니다. 특히 **모바일 환경에서의 사용성**에 초점을 맞춰, 터치 인터페이스에 최적화된 반응형 디자인과 불필요한 UI 요소 제거를 통해 간결하고 효율적인 사용자 인터페이스를 구현합니다.

## 2. 현재 상태 및 문제점

### 문제점
1. **모바일 가독성 부족**: 글자 크기와 UI 요소가 작아서 모바일 기기에서 사용이 어려움
2. **중복된 상태 표시**: Copilot 화면의 status bar와 top bar에 정보가 분산되어 있음
3. **불필요한 UI 요소**: 대부분의 화면에서 사용되지 않는 인증 정보와 로그아웃 버튼이 표시됨
4. **작업 디렉토리 설정 불편**: Copilot 설정 패널에서 경로를 직접 입력해야 하는 번거로움
5. **불필요한 홈 화면**: 단순히 링크만 보여주는 홈 화면이 사용자 흐름을 방해함

### 영향
- 모바일 사용자의 이탈
- 작업 효율성 저하
- 불필요한 클릭/터치 증가

## 3. 목표

### 주요 목표
1. **모바일 친화적 UI**: 터치 인터페이스에 최적화된 반응형 디자인 구현
2. **UI 통합 및 간소화**: 중복된 정보 표시 제거, 필요한 곳에만 적절한 UI 표시
3. **직관적인 경로 선택**: 시각적 폴더 브라우저를 통한 쉬운 디렉토리 선택
4. **빠른 접근성**: 홈 화면 제거로 핵심 기능에 즉시 접근

### 성공 기준
- [ ] 모바일 기기에서 모든 텍스트가 확대 없이 읽기 가능
- [ ] 터치 타겟 크기가 최소 44x44px 이상 (iOS/Android 권장 사이즈)
- [ ] Copilot 화면의 상태 정보가 top bar에 통합되어 화면 공간 확보
- [ ] Settings를 제외한 화면에서 불필요한 top bar 제거
- [ ] 폴더 브라우저를 통해 3번의 터치 이내로 작업 디렉토리 선택 가능
- [ ] 앱 시작 시 Copilot 화면으로 직접 이동

## 4. 접근 방법

### 4.1 모바일 반응형 UI (우선순위: 높음)

**설계 방향**
- Bootstrap의 반응형 그리드 시스템 활용 (이미 적용된 Bootstrap 5 유지)
- CSS 미디어 쿼리로 breakpoint별 스타일 정의
- 모바일 우선(Mobile-first) 접근 방식

**주요 변경사항**
```
[데스크톱]                [모바일]
- 기본 폰트: 14px    →   - 기본 폰트: 16px
- 버튼 높이: 38px    →   - 버튼 높이: 48px
- 입력 필드: 38px    →   - 입력 필드: 48px
- 여백/패딩: 1rem    →   - 여백/패딩: 1.5rem
- 아이콘: 16px       →   - 아이콘: 24px
```

**구현 위치**
- `wwwroot/app.css`: 반응형 스타일 추가
- 모든 `.razor` 컴포넌트의 inline style 정리

**기술적 제약**
- Blazor Server의 렌더링 특성상 클라이언트 사이드 resize 이벤트에 주의
- `@media` 쿼리만으로 처리 가능한 범위 우선 처리

### 4.2 Copilot 화면 Status Bar 통합 (우선순위: 높음)

**현재 구조**
```
[Top Bar]
  - Authenticated: user@example.com
  - Logout 버튼

[Status Bar]
  - 연결 상태 (Connected/Disconnected)
  - 사용자 정보 (copilotUser)
  - 모델 정보 (currentModel)
  - Settings 버튼
  - 모델 선택 드롭다운
  - Check Status 버튼
```

**개선 후 구조**
```
[통합 Top Bar]
  - 🟢 Connected • Model: gpt-4 [모델 선택 ▼] [⚙️ Settings] [🔄 Check Status]
```

**구현 방법**
- `Components/Pages/Copilot.razor`: status bar 섹션 제거, top bar로 이동
- `Components/Layout/MainLayout.razor`: Copilot 페이지에서만 동적으로 확장된 top bar 렌더링
- CSS flexbox로 좁은 화면에서 자동 줄바꿈 처리

**트레이드오프**
| 방식 | 장점 | 단점 | 선택 |
|------|------|------|------|
| 페이지 내 자체 top bar | 독립성 유지 | 레이아웃 일관성 부족 | ❌ |
| MainLayout 동적 확장 | 일관된 레이아웃 | 복잡도 증가 | ✅ |
| 별도 CopilotLayout | 깔끔한 분리 | 파일 증가 | ⚠️ (대안) |

### 4.3 Top Bar 조건부 렌더링 (우선순위: 중간)

**제거 대상 화면**
- `/copilot`: status bar와 통합으로 대체 (4.2)
- `/terminal`: 전체 화면 터미널 경험 제공
- `/file-browser`: 파일 목록에 집중

**유지할 화면**
- `/settings`: 인증 정보와 로그아웃 필요

**구현 방법**
```
MainLayout.razor:
1. NavigationManager로 현재 경로 감지
2. 경로별로 top bar 표시 여부 결정
3. Copilot 화면은 확장된 top bar, 나머지는 최소화 또는 제거
```

**고려 사항**
- 로그아웃 기능 접근성: Settings 페이지로 이동하거나, 사이드바 메뉴에 추가
- 빈 top bar일 경우 `display: none`으로 완전 제거하여 공간 확보

### 4.4 폴더 브라우저 UI (우선순위: 중간)

**현재 방식**: 텍스트 입력 필드에 절대 경로 입력
```
[작업 디렉토리: /Users/username/Documents/project    ]
```

**개선 방식**: 시각적 폴더 탐색기
```
┌─ 작업 디렉토리 선택 ────────────────┐
│ 📁 Documents (기본 경로)             │
│   📁 Projects                        │
│   📁 Notes                           │
│   📁 work                            │
│     📁 MobileAICLI         [선택]    │
│     📁 other-project                 │
│                                      │
│ [취소] [선택한 경로로 설정]          │
└──────────────────────────────────────┘
```

**구현 전략**
1. **Phase 1**: Modal 다이얼로그로 폴더 선택 UI 추가
   - 기본 시작 경로: `Environment.SpecialFolder.MyDocuments`
   - 서버 사이드에서 폴더 목록 API 제공 (FileService 확장)
   - 부모 폴더 이동, 서브폴더 진입 기능

2. **Phase 2**: 빵부스러기(Breadcrumb) 네비게이션 추가
   - `/Users/username/Documents/work/MobileAICLI` → 각 단계 클릭 가능

**보안 고려사항**
- AllowedWorkRoots 범위 내에서만 탐색 허용
- 심볼릭 링크 탐지 및 차단
- 숨김 파일/시스템 폴더 필터링

**API 설계**
```csharp
// SettingsHub 또는 FileService
public async Task<FolderBrowserResult> ListDirectories(string parentPath)
{
    // 1. 보안 검증 (AllowedWorkRoots)
    // 2. Directory.GetDirectories() 호출
    // 3. 폴더명, 경로, 접근 가능 여부 반환
}
```

**UI 컴포넌트**
- 새 파일 생성: `Components/Shared/FolderBrowser.razor`
- Copilot 설정 패널에서 사용

### 4.5 홈 화면 제거 및 기본 경로 변경 (우선순위: 낮음)

**현재**: `/` → `Home.razor` (환영 메시지 + 링크)  
**개선**: `/` → `/copilot`으로 리다이렉트

**구현 방법**

**옵션 A: 라우팅 변경**
```csharp
// Home.razor 삭제
// Copilot.razor에 @page "/" 추가 (기존 @page "/copilot"과 병행)
```

**옵션 B: 리다이렉트 컴포넌트**
```csharp
// Home.razor
@page "/"
@code {
    protected override void OnInitialized()
    {
        NavigationManager.NavigateTo("/copilot");
    }
}
```

**선택**: **옵션 A** (더 간단하고 깔끔)

**부가 효과**
- 사이드바 메뉴에서 "Home" 링크 제거
- NavMenu.razor 업데이트

## 5. 작업 단계

### Phase 1: 기반 작업 (1일)
- [ ] 반응형 CSS 스타일 작성 (`app.css` 확장)
- [ ] 모바일 breakpoint 정의 및 공통 클래스 추가
- [ ] 기존 컴포넌트들의 기본 폰트/버튼 크기 조정

### Phase 2: Copilot 화면 개선 (1일)
- [ ] Status bar 내용을 top bar로 이동
- [ ] MainLayout에서 Copilot 페이지 감지 로직 추가
- [ ] 통합된 top bar 스타일 적용
- [ ] 모바일에서 레이아웃 검증

### Phase 3: Top Bar 조건부 렌더링 (0.5일)
- [ ] NavigationManager로 경로별 top bar 제어
- [ ] Settings 외 화면에서 인증 정보 제거
- [ ] 사이드바 메뉴 조정 (필요 시 로그아웃 추가)

### Phase 4: 폴더 브라우저 구현 (2일)
- [ ] FolderBrowser.razor 컴포넌트 생성
- [ ] FileService에 폴더 목록 API 추가 (보안 검증 포함)
- [ ] Modal 다이얼로그 UI 작성
- [ ] Copilot 설정 패널에 통합
- [ ] 모바일 터치 인터랙션 테스트

### Phase 5: 홈 화면 제거 (0.5일)
- [ ] `Home.razor` 삭제 또는 리다이렉트 전환
- [ ] `Copilot.razor`에 `@page "/"` 추가
- [ ] `NavMenu.razor`에서 Home 링크 제거
- [ ] 전체 네비게이션 흐름 테스트

### Phase 6: 통합 테스트 및 검증 (1일)
- [ ] 모바일 실기기 테스트 (iOS/Android)
- [ ] 데스크톱 브라우저 호환성 확인
- [ ] 터치 타겟 크기 검증 (Chrome DevTools)
- [ ] 성능 테스트 (반응형 렌더링 속도)
- [ ] 사용성 피드백 수렴

## 6. 일정

| Phase | 작업 내용 | 예상 기간 |
|-------|----------|----------|
| Phase 1 | 반응형 CSS 기반 작업 | 1일 |
| Phase 2 | Copilot 화면 개선 | 1일 |
| Phase 3 | Top Bar 조건부 렌더링 | 0.5일 |
| Phase 4 | 폴더 브라우저 구현 | 2일 |
| Phase 5 | 홈 화면 제거 | 0.5일 |
| Phase 6 | 통합 테스트 | 1일 |
| **총계** | | **6일** |

### 마일스톤
- **Week 1 완료 (Day 3)**: Phase 1~3 완료 → 기본 반응형 UI 적용, Copilot 화면 개선
- **Week 2 완료 (Day 6)**: Phase 4~6 완료 → 폴더 브라우저 완성, 전체 검증

## 7. 위험 요소 및 대응

| 위험 | 발생 가능성 | 영향도 | 대응 방안 |
|------|------------|--------|----------|
| 모바일 브라우저 호환성 문제 | 중간 | 높음 | 주요 브라우저(Safari, Chrome Mobile) 우선 테스트 |
| 폴더 브라우저 성능 저하 | 낮음 | 중간 | 폴더 깊이 제한, 페이지네이션 고려 |
| Blazor Server 렌더링 지연 | 중간 | 중간 | 로딩 인디케이터 추가, 클라이언트 캐싱 |
| 기존 레이아웃 깨짐 | 낮음 | 높음 | 단계별 배포, 롤백 계획 수립 |

## 8. 기술 상세

### 8.1 반응형 Breakpoints

```css
/* app.css에 추가할 미디어 쿼리 */

/* Mobile: < 768px (Bootstrap sm) */
@media (max-width: 767.98px) {
  body { font-size: 16px; }
  .btn { min-height: 48px; padding: 12px 20px; }
  .form-control { min-height: 48px; font-size: 16px; }
  .container-fluid { padding: 1.5rem; }
}

/* Tablet: 768px ~ 991px (Bootstrap md) */
@media (min-width: 768px) and (max-width: 991.98px) {
  body { font-size: 15px; }
  .btn { min-height: 44px; }
}

/* Desktop: >= 992px (Bootstrap lg) */
@media (min-width: 992px) {
  body { font-size: 14px; }
  /* 기본 스타일 유지 */
}
```

### 8.2 Top Bar 동적 제어

```razor
<!-- MainLayout.razor 예시 -->
@inject NavigationManager Navigation

@code {
    private bool ShouldShowTopBar()
    {
        var path = Navigation.ToBaseRelativePath(Navigation.Uri);
        // Settings 화면에만 표시
        return path.StartsWith("settings");
    }
    
    private bool IsCopilotPage()
    {
        var path = Navigation.ToBaseRelativePath(Navigation.Uri);
        return path == "" || path.StartsWith("copilot");
    }
}
```

### 8.3 폴더 브라우저 데이터 모델

```csharp
// Models/FolderBrowserResult.cs
public class FolderBrowserResult
{
    public string CurrentPath { get; set; } = "";
    public List<FolderItem> Folders { get; set; } = new();
    public string? Error { get; set; }
}

public class FolderItem
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public bool IsAccessible { get; set; } = true;
}
```

### 8.4 보안 검증 로직

```csharp
// FileService.cs 확장
public FolderBrowserResult GetSubfolders(string parentPath)
{
    // 1. AllowedWorkRoots 검증
    if (!IsPathAllowed(parentPath))
    {
        return new FolderBrowserResult 
        { 
            Error = "Access denied" 
        };
    }
    
    // 2. 실제 경로 확인 (심볼릭 링크 해결)
    var realPath = Path.GetFullPath(parentPath);
    
    // 3. 하위 폴더 목록 반환
    // ...
}
```

## 9. 참고 자료

- [Bootstrap 5 Responsive Design](https://getbootstrap.com/docs/5.0/layout/breakpoints/)
- [iOS Human Interface Guidelines - Touch Targets](https://developer.apple.com/design/human-interface-guidelines/ios/visual-design/adaptivity-and-layout/)
- [Material Design - Touch Targets](https://material.io/design/usability/accessibility.html#layout-and-typography)
- [Blazor Server Performance Best Practices](https://learn.microsoft.com/en-us/aspnet/core/blazor/performance)

---

## 변경 이력

| 날짜 | 버전 | 변경 내용 |
|------|------|----------|
| 2024-12-07 | 1.0 | 초안 작성 |

---

*이 문서는 [DOCUMENTATION_GUIDELINES.md](../DOCUMENTATION_GUIDELINES.md)를 따라 작성되었습니다.*
