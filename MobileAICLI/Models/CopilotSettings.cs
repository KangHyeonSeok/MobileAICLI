namespace MobileAICLI.Models;

/// <summary>
/// Copilot 도구 권한 및 인증 설정
/// Phase 1.2: Copilot 설정 패널
/// </summary>
public class CopilotSettings
{
    // 전체 허용 (위험)
    public bool AllowAll { get; set; }
    
    // 파일 작업
    public bool FileRead { get; set; } = true;
    public bool FileWrite { get; set; } = true;
    public bool FileDelete { get; set; }
    
    // Git 작업
    public bool GitStatus { get; set; } = true;
    public bool GitBranch { get; set; } = true;
    public bool GitCommit { get; set; } = true;
    public bool GitPush { get; set; }
    public bool GitForce { get; set; }
    
    // PR/Issue
    public bool PrRead { get; set; } = true;
    public bool PrWrite { get; set; }
    public bool IssueWrite { get; set; }
    
    // 쉘 명령
    public bool ShellBasic { get; set; } = true;
    public bool ShellPackage { get; set; }
    public bool ShellSystem { get; set; }
    
    // GitHub 토큰 인증 방식
    public GithubTokenMode TokenMode { get; set; } = GithubTokenMode.System;
    public string TokenValue { get; set; } = string.Empty;
    
    // 작업 디렉토리
    public string WorkingDirectory { get; set; } = string.Empty;

    /// <summary>
    /// 프리셋 적용
    /// </summary>
    public void ApplyPreset(string preset)
    {
        switch (preset.ToLowerInvariant())
        {
            case "safe":
                ApplySafePreset();
                break;
            case "medium":
                ApplyMediumPreset();
                break;
            case "full":
                ApplyFullPreset();
                break;
            default:
                ApplyMediumPreset(); // 기본값
                break;
        }
    }

    private void ApplySafePreset()
    {
        AllowAll = false;
        FileRead = true;
        FileWrite = false;
        FileDelete = false;
        GitStatus = false;
        GitBranch = false;
        GitCommit = false;
        GitPush = false;
        GitForce = false;
        PrRead = false;
        PrWrite = false;
        IssueWrite = false;
        ShellBasic = false;
        ShellPackage = false;
        ShellSystem = false;
    }

    private void ApplyMediumPreset()
    {
        AllowAll = false;
        FileRead = true;
        FileWrite = true;
        FileDelete = false;
        GitStatus = true;
        GitBranch = true;
        GitCommit = true;
        GitPush = false;
        GitForce = false;
        PrRead = true;
        PrWrite = false;
        IssueWrite = false;
        ShellBasic = true;
        ShellPackage = false;
        ShellSystem = false;
    }

    private void ApplyFullPreset()
    {
        AllowAll = true;
        // AllowAll이 true일 때는 개별 설정이 무시됨
    }

    /// <summary>
    /// CLI 옵션 문자열 생성
    /// </summary>
    public string BuildCliOptions()
    {
        if (AllowAll)
        {
            return "--allow-all-tools";
        }

        var options = new List<string>();

        // 허용된 도구들 추가
        if (FileRead)
        {
            options.Add("--allow-tool 'shell(cat,head,tail,less)'");
        }
        
        if (FileWrite)
        {
            options.Add("--allow-tool 'write'");
        }
        
        if (FileDelete)
        {
            options.Add("--allow-tool 'shell(rm)'");
        }
        
        if (GitStatus)
        {
            options.Add("--allow-tool 'shell(git status,git log,git diff)'");
        }
        
        if (GitBranch)
        {
            options.Add("--allow-tool 'shell(git branch,git checkout)'");
        }
        
        if (GitCommit)
        {
            options.Add("--allow-tool 'shell(git add,git commit)'");
        }
        
        if (GitPush)
        {
            options.Add("--allow-tool 'shell(git push)'");
        }
        
        if (GitForce)
        {
            options.Add("--allow-tool 'shell(git reset)'");
        }
        
        if (PrRead)
        {
            options.Add("--allow-tool 'shell(gh pr list,gh issue list)'");
        }
        
        if (PrWrite)
        {
            options.Add("--allow-tool 'shell(gh pr create)'");
        }
        
        if (IssueWrite)
        {
            options.Add("--allow-tool 'shell(gh issue create)'");
        }
        
        if (ShellBasic)
        {
            options.Add("--allow-tool 'shell(ls,pwd,echo,find)'");
        }
        
        if (ShellPackage)
        {
            options.Add("--allow-tool 'shell(npm,pip,yarn)'");
        }
        
        if (ShellSystem)
        {
            options.Add("--allow-tool 'shell'");
        }

        return string.Join(" ", options);
    }

    /// <summary>
    /// GitHub 토큰 환경변수 주입 방식 결정
    /// </summary>
    public (string? Key, string? Value) GetGithubTokenEnv()
    {
        return TokenMode switch
        {
            GithubTokenMode.System => (null, null), // 시스템 환경변수 사용
            GithubTokenMode.Custom => ("GITHUB_TOKEN", TokenValue),
            GithubTokenMode.None => ("GITHUB_TOKEN", string.Empty), // 빈 문자열로 설정하여 무시
            _ => (null, null)
        };
    }
}

/// <summary>
/// GitHub 토큰 인증 모드
/// </summary>
public enum GithubTokenMode
{
    /// <summary>시스템 환경변수 사용 (기본)</summary>
    System,
    
    /// <summary>사용자 직접 입력</summary>
    Custom,
    
    /// <summary>토큰 없이 실행</summary>
    None
}
