namespace MobileAICLI.Models;

/// <summary>
/// Represents branch information in a Git repository
/// </summary>
public class GitBranchInfo
{
    /// <summary>
    /// Name of the branch
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether this is the currently checked out branch
    /// </summary>
    public bool IsCurrentBranch { get; set; }
    
    /// <summary>
    /// Whether this is a remote tracking branch
    /// </summary>
    public bool IsRemote { get; set; }
    
    /// <summary>
    /// Name of the upstream branch if any
    /// </summary>
    public string? UpstreamBranch { get; set; }
    
    /// <summary>
    /// Number of commits ahead of upstream
    /// </summary>
    public int AheadBy { get; set; }
    
    /// <summary>
    /// Number of commits behind upstream
    /// </summary>
    public int BehindBy { get; set; }
    
    /// <summary>
    /// Time of the last commit on this branch
    /// </summary>
    public DateTime? LastCommitTime { get; set; }
    
    /// <summary>
    /// Message of the last commit on this branch
    /// </summary>
    public string? LastCommitMessage { get; set; }
}
