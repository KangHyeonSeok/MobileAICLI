namespace MobileAICLI.Models;

/// <summary>
/// Represents a changed file in a Git repository
/// </summary>
public class GitFileChange
{
    /// <summary>
    /// Full path of the file relative to repository root
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Just the file name without path
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of change (Added, Modified, Deleted, etc.)
    /// </summary>
    public GitChangeType ChangeType { get; set; }
    
    /// <summary>
    /// Whether the file is staged for commit
    /// </summary>
    public bool IsStaged { get; set; }
    
    /// <summary>
    /// Number of lines added
    /// </summary>
    public int AddedLines { get; set; }
    
    /// <summary>
    /// Number of lines deleted
    /// </summary>
    public int DeletedLines { get; set; }
}

/// <summary>
/// Type of change for a file in Git
/// </summary>
public enum GitChangeType
{
    Added,
    Modified,
    Deleted,
    Renamed,
    Copied,
    Untracked,
    Unmerged
}
