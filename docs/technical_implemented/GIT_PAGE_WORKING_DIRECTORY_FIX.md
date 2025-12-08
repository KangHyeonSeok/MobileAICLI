# Git 페이지 경로 변경 문제 해결

## 문제 상황
Git 페이지에서 폴더 브라우저를 통해 경로를 변경해도 "Not a Git repository" 메시지가 표시되고, 수정 사항이나 브랜치 정보가 표시되지 않는 문제가 발생했습니다.

## 원인 분석
Blazor Server의 Scoped 서비스 특성으로 인해:
- Blazor 컴포넌트(`Git.razor`)가 주입받는 `RepositoryContext`와
- SignalR Hub(`GitHub`)가 각 호출마다 생성하는 `RepositoryContext`가 **서로 다른 인스턴스**

따라서 Blazor 컴포넌트에서 `RepoContext.ChangeRootAsync()`로 경로를 변경해도, Hub를 통해 호출되는 `GitService`는 이전 경로(또는 기본 경로)를 사용하게 됩니다.

## 해결 방법
Copilot 서비스의 패턴을 따라 **명시적으로 workingDirectory 파라미터를 전달**하는 방식으로 변경:

### 1. GitService 메서드에 workingDirectory 파라미터 추가
모든 public 메서드에 `string? workingDirectory = null` 파라미터를 추가하여, 명시적으로 경로를 전달받을 수 있도록 함.

```csharp
public async Task<GitRepositoryStatus> GetRepositoryStatusAsync(string? workingDirectory = null)
public async Task<List<GitFileChange>> GetChangedFilesAsync(string? workingDirectory = null)
// ... 모든 public 메서드
```

### 2. 경로 검증 메서드 추가
보안을 위해 전달받은 경로를 검증하는 메서드 추가:

```csharp
private string ValidateAndGetWorkingDirectory(string? workingDirectory)
{
    if (string.IsNullOrWhiteSpace(workingDirectory))
        return _context.GetAbsolutePath();
    
    var normalizedPath = Path.GetFullPath(workingDirectory);
    if (!Directory.Exists(normalizedPath))
        return _context.GetAbsolutePath();
    
    return normalizedPath;
}
```

### 3. GitHub Hub 메서드 업데이트
Hub의 모든 메서드에 workingDirectory 파라미터 추가하고 GitService 호출 시 전달:

```csharp
public async Task<GitRepositoryStatus> GetStatus(string? workingDirectory = null)
{
    return await _gitService.GetRepositoryStatusAsync(workingDirectory);
}
```

### 4. Git.razor에서 Hub 호출 시 경로 전달
Hub 메서드를 호출할 때 `currentRepoPath` 전달:

```csharp
status = await hubConnection.InvokeAsync<GitRepositoryStatus>("GetStatus", currentRepoPath);
changedFiles = await hubConnection.InvokeAsync<List<GitFileChange>>("GetChangedFiles", currentRepoPath);
```

### 5. GitBranchModal 업데이트
브랜치 모달에도 WorkingDirectory 파라미터 추가 및 Hub 호출 시 전달.

## 보안 고려사항

### 경로 검증
- `Path.GetFullPath()`로 경로 정규화
- 디렉토리 존재 여부 확인
- 잘못된 경로는 기본 context 경로로 폴백
- 로그를 통해 비정상 경로 접근 추적

### 향후 개선 가능 사항
- 경로 화이트리스트 검증 추가 (`AllowedRepositoryRoots`)
- 심볼릭 링크 탈출 방지 강화
- 경로 이탈(../) 명시적 차단

## 영향 범위
- `Services/GitService.cs`: 모든 public 메서드 시그니처 변경
- `Hubs/GitHub.cs`: 모든 hub 메서드 시그니처 변경
- `Components/Pages/Git.razor`: Hub 호출 부분 수정
- `Components/Pages/GitBranchModal.razor`: 파라미터 추가 및 Hub 호출 수정

## 테스트
- 빌드 성공 확인
- 기존 단위 테스트 통과 (5/5 passed)
- 수동 테스트 필요: 실제 Git 저장소 경로 변경 시나리오

## 참고
이 패턴은 기존 `CopilotService`와는 다르게, 각 메서드에 `workingDirectory` 파라미터를 명시적으로 전달하는 방식입니다. `CopilotService`는 메서드 파라미터로 경로를 받지 않고, 내부적으로 `_context.GetAbsolutePath()`를 사용합니다.
