namespace MobileAICLI.Models;

/// <summary>
/// Result of a Git diff operation
/// </summary>
public class GitDiffResult
{
    /// <summary>
    /// Path of the file being diffed
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Raw diff content as string
    /// </summary>
    public string RawDiff { get; set; } = string.Empty;
    
    /// <summary>
    /// Parsed diff hunks
    /// </summary>
    public List<DiffHunk> Hunks { get; set; } = new();
}

/// <summary>
/// Represents a single hunk in a diff
/// </summary>
public class DiffHunk
{
    /// <summary>
    /// Starting line number in the old file
    /// </summary>
    public int OldStart { get; set; }
    
    /// <summary>
    /// Number of lines in the old file for this hunk
    /// </summary>
    public int OldLines { get; set; }
    
    /// <summary>
    /// Starting line number in the new file
    /// </summary>
    public int NewStart { get; set; }
    
    /// <summary>
    /// Number of lines in the new file for this hunk
    /// </summary>
    public int NewLines { get; set; }
    
    /// <summary>
    /// Header text of the hunk (e.g., function name)
    /// </summary>
    public string? Header { get; set; }
    
    /// <summary>
    /// Lines in this hunk
    /// </summary>
    public List<DiffLine> Lines { get; set; } = new();
}

/// <summary>
/// Represents a single line in a diff
/// </summary>
public class DiffLine
{
    /// <summary>
    /// Type of diff line (Added, Deleted, or Context)
    /// </summary>
    public DiffLineType Type { get; set; }
    
    /// <summary>
    /// Content of the line
    /// </summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// Line number in the old file (null for added lines)
    /// </summary>
    public int? OldLineNumber { get; set; }
    
    /// <summary>
    /// Line number in the new file (null for deleted lines)
    /// </summary>
    public int? NewLineNumber { get; set; }
}

/// <summary>
/// Type of a diff line
/// </summary>
public enum DiffLineType
{
    /// <summary>
    /// Unchanged context line
    /// </summary>
    Context,
    
    /// <summary>
    /// Added line
    /// </summary>
    Added,
    
    /// <summary>
    /// Deleted line
    /// </summary>
    Deleted
}
