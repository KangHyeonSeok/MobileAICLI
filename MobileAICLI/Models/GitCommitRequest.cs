namespace MobileAICLI.Models;

/// <summary>
/// Request DTO for creating a Git commit
/// </summary>
public class GitCommitRequest
{
    /// <summary>
    /// Commit message summary (required)
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Extended commit description (optional)
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Files to stage before committing. If null, uses currently staged files.
    /// </summary>
    public List<string>? FilesToStage { get; set; }
}
