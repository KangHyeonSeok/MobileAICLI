namespace MobileAICLI.Models;

/// <summary>
/// Represents the status of a Git repository
/// </summary>
public class GitRepositoryStatus
{
    /// <summary>
    /// Whether the current directory is a Git repository
    /// </summary>
    public bool IsRepository { get; set; }
    
    /// <summary>
    /// Name of the current branch
    /// </summary>
    public string? CurrentBranch { get; set; }
    
    /// <summary>
    /// Whether there are uncommitted changes
    /// </summary>
    public bool HasUncommittedChanges { get; set; }
    
    /// <summary>
    /// Number of commits ahead of upstream
    /// </summary>
    public int AheadBy { get; set; }
    
    /// <summary>
    /// Number of commits behind upstream
    /// </summary>
    public int BehindBy { get; set; }
    
    /// <summary>
    /// Remote origin URL
    /// </summary>
    public string? RemoteUrl { get; set; }
    
    /// <summary>
    /// Whether Git authentication is properly configured
    /// </summary>
    public bool IsAuthenticated { get; set; }
    
    /// <summary>
    /// Authentication status message
    /// </summary>
    public string? AuthenticationMessage { get; set; }
    
    /// <summary>
    /// Last fetch time
    /// </summary>
    public DateTime? LastFetchTime { get; set; }
    
    /// <summary>
    /// Error message if any
    /// </summary>
    public string? Error { get; set; }
}
