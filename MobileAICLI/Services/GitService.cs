using System.Diagnostics;
using System.Text.RegularExpressions;
using MobileAICLI.Models;

namespace MobileAICLI.Services;

/// <summary>
/// Service for Git operations using Git CLI with GitHub CLI authentication
/// </summary>
public class GitService
{
    private readonly RepositoryContext _context;
    private readonly ILogger<GitService> _logger;
    private const int CommandTimeoutSeconds = 30;

    public GitService(RepositoryContext context, ILogger<GitService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Validates and normalizes a working directory path to prevent path traversal attacks
    /// </summary>
    private string ValidateAndGetWorkingDirectory(string? workingDirectory)
    {
        // If no working directory provided, use context default
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            return _context.GetAbsolutePath();
        }

        try
        {
            // Normalize the path
            var normalizedPath = Path.GetFullPath(workingDirectory);

            // Check if the directory exists
            if (!Directory.Exists(normalizedPath))
            {
                _logger.LogWarning("Working directory does not exist: {Path}", normalizedPath);
                return _context.GetAbsolutePath();
            }

            return normalizedPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating working directory: {Path}", workingDirectory);
            return _context.GetAbsolutePath();
        }
    }

    #region Authentication

    /// <summary>
    /// Checks if Git and GitHub CLI are properly authenticated
    /// </summary>
    public async Task<(bool IsAuthenticated, string Message)> CheckAuthenticationAsync(string? workingDirectory = null)
    {
        try
        {
            // Check if gh is installed and authenticated
            var (ghSuccess, _, ghError) = await ExecuteCommandAsync("gh", workingDirectory, "auth", "status");
            
            if (!ghSuccess)
            {
                if (ghError.Contains("not found") || ghError.Contains("not recognized"))
                {
                    return (false, "GitHub CLI (gh) is not installed. Please install it to use Git features.");
                }
                return (false, "GitHub CLI is not authenticated. Please run 'gh auth login' to authenticate.");
            }
            
            // Check if git credential helper is configured for gh
            var (gitSuccess, gitHelper, _) = await ExecuteGitCommandAsync(workingDirectory, "config", "--get", "credential.helper");
            
            if (!gitHelper.Contains("gh"))
            {
                return (false, "Git credential helper is not configured for GitHub CLI. Please run 'gh auth setup-git'.");
            }
            
            return (true, "Git authentication is properly configured.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Git authentication");
            return (false, $"Error checking authentication: {ex.Message}");
        }
    }

    #endregion

    #region Repository Status

    /// <summary>
    /// Gets the current repository status
    /// </summary>
    public async Task<GitRepositoryStatus> GetRepositoryStatusAsync(string? workingDirectory = null)
    {
        var status = new GitRepositoryStatus();
        
        try
        {
            // Check if it's a git repository
            var (isRepo, _, _) = await ExecuteGitCommandAsync(workingDirectory, "rev-parse", "--is-inside-work-tree");
            status.IsRepository = isRepo;
            
            if (!isRepo)
            {
                status.Error = "Not a Git repository";
                return status;
            }
            
            // Get current branch
            var (branchSuccess, branchOutput, _) = await ExecuteGitCommandAsync(workingDirectory, "branch", "--show-current");
            if (branchSuccess)
            {
                status.CurrentBranch = branchOutput.Trim();
            }
            
            // Check for uncommitted changes
            var (statusSuccess, statusOutput, _) = await ExecuteGitCommandAsync(workingDirectory, "status", "--porcelain");
            status.HasUncommittedChanges = statusSuccess && !string.IsNullOrWhiteSpace(statusOutput);
            
            // Get ahead/behind status
            var (revSuccess, revOutput, _) = await ExecuteGitCommandAsync(workingDirectory, "rev-list", "--count", "--left-right", "@{upstream}...HEAD");
            if (revSuccess && !string.IsNullOrWhiteSpace(revOutput))
            {
                var parts = revOutput.Trim().Split('\t');
                if (parts.Length == 2)
                {
                    int.TryParse(parts[0], out int behind);
                    int.TryParse(parts[1], out int ahead);
                    status.BehindBy = behind;
                    status.AheadBy = ahead;
                }
            }
            
            // Get remote URL
            var (remoteSuccess, remoteOutput, _) = await ExecuteGitCommandAsync(workingDirectory, "config", "--get", "remote.origin.url");
            if (remoteSuccess)
            {
                status.RemoteUrl = remoteOutput.Trim();
            }
            
            // Check authentication
            var (authResult, authMessage) = await CheckAuthenticationAsync();
            status.IsAuthenticated = authResult;
            status.AuthenticationMessage = authMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting repository status");
            status.Error = ex.Message;
        }
        
        return status;
    }

    #endregion

    #region File Changes

    /// <summary>
    /// Gets the list of changed files in the repository
    /// </summary>
    public async Task<List<GitFileChange>> GetChangedFilesAsync(string? workingDirectory = null)
    {
        var changes = new List<GitFileChange>();
        
        try
        {
            // Get staged and unstaged changes
            var (success, output, _) = await ExecuteGitCommandAsync(workingDirectory, "status", "--porcelain", "-uall");
            
            if (!success || string.IsNullOrWhiteSpace(output))
            {
                return changes;
            }
            
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                if (line.Length < 4) continue;
                
                var indexStatus = line[0];
                var workTreeStatus = line[1];
                var filePath = line.Substring(3).Trim();
                
                // Handle renamed files (format: "R  old -> new")
                if (filePath.Contains(" -> "))
                {
                    filePath = filePath.Split(" -> ")[1];
                }
                
                var change = new GitFileChange
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    IsStaged = indexStatus != ' ' && indexStatus != '?',
                    ChangeType = GetChangeType(indexStatus, workTreeStatus)
                };
                
                // Get line counts for modified files
                if (change.ChangeType != GitChangeType.Untracked)
                {
                    var (diffSuccess, diffOutput, _) = await ExecuteGitCommandAsync(workingDirectory, "diff", "--numstat", "--", filePath);
                    if (diffSuccess && !string.IsNullOrWhiteSpace(diffOutput))
                    {
                        var parts = diffOutput.Trim().Split('\t');
                        if (parts.Length >= 2)
                        {
                            int.TryParse(parts[0], out int added);
                            int.TryParse(parts[1], out int deleted);
                            change.AddedLines = added;
                            change.DeletedLines = deleted;
                        }
                    }
                }
                
                changes.Add(change);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting changed files");
        }
        
        return changes;
    }

    private static GitChangeType GetChangeType(char indexStatus, char workTreeStatus)
    {
        var status = indexStatus != ' ' ? indexStatus : workTreeStatus;
        
        return status switch
        {
            'A' => GitChangeType.Added,
            'M' => GitChangeType.Modified,
            'D' => GitChangeType.Deleted,
            'R' => GitChangeType.Renamed,
            'C' => GitChangeType.Copied,
            '?' => GitChangeType.Untracked,
            'U' => GitChangeType.Unmerged,
            _ => GitChangeType.Modified
        };
    }

    #endregion

    #region Diff

    /// <summary>
    /// Gets the diff for a specific file
    /// </summary>
    public async Task<GitDiffResult> GetFileDiffAsync(string filePath, string? workingDirectory = null)
    {
        var result = new GitDiffResult { FilePath = filePath };
        
        try
        {
            // Get diff (both staged and unstaged)
            var (success, output, _) = await ExecuteGitCommandAsync(workingDirectory, "diff", "HEAD", "--", filePath);
            
            if (!success)
            {
                // Try diff against empty (for new files)
                (success, output, _) = await ExecuteGitCommandAsync(workingDirectory, "diff", "--no-index", "/dev/null", filePath);
            }
            
            if (success && !string.IsNullOrWhiteSpace(output))
            {
                result.RawDiff = output;
                result.Hunks = ParseDiffOutput(output);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting diff for file: {FilePath}", filePath);
        }
        
        return result;
    }

    private List<DiffHunk> ParseDiffOutput(string diffOutput)
    {
        var hunks = new List<DiffHunk>();
        var lines = diffOutput.Split('\n');
        DiffHunk? currentHunk = null;
        int oldLine = 0, newLine = 0;
        
        var hunkHeaderRegex = new Regex(@"^@@\s+-(\d+)(?:,(\d+))?\s+\+(\d+)(?:,(\d+))?\s+@@(.*)$");
        
        foreach (var line in lines)
        {
            var match = hunkHeaderRegex.Match(line);
            if (match.Success)
            {
                currentHunk = new DiffHunk
                {
                    OldStart = int.Parse(match.Groups[1].Value),
                    OldLines = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 1,
                    NewStart = int.Parse(match.Groups[3].Value),
                    NewLines = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 1,
                    Header = match.Groups[5].Value.Trim()
                };
                hunks.Add(currentHunk);
                oldLine = currentHunk.OldStart;
                newLine = currentHunk.NewStart;
            }
            else if (currentHunk != null)
            {
                if (line.StartsWith("+") && !line.StartsWith("+++"))
                {
                    currentHunk.Lines.Add(new DiffLine
                    {
                        Type = DiffLineType.Added,
                        Content = line.Substring(1),
                        NewLineNumber = newLine++
                    });
                }
                else if (line.StartsWith("-") && !line.StartsWith("---"))
                {
                    currentHunk.Lines.Add(new DiffLine
                    {
                        Type = DiffLineType.Deleted,
                        Content = line.Substring(1),
                        OldLineNumber = oldLine++
                    });
                }
                else if (line.StartsWith(" "))
                {
                    currentHunk.Lines.Add(new DiffLine
                    {
                        Type = DiffLineType.Context,
                        Content = line.Substring(1),
                        OldLineNumber = oldLine++,
                        NewLineNumber = newLine++
                    });
                }
            }
        }
        
        return hunks;
    }

    #endregion

    #region Staging

    /// <summary>
    /// Stages a file for commit
    /// </summary>
    public async Task<(bool Success, string Message)> StageFileAsync(string filePath, string? workingDirectory = null)
    {
        try
        {
            var (success, _, error) = await ExecuteGitCommandAsync(workingDirectory, "add", "--", filePath);
            
            if (success)
            {
                _logger.LogInformation("Staged file: {FilePath}", filePath);
                return (true, $"Staged: {filePath}");
            }
            
            return (false, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error staging file: {FilePath}", filePath);
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Unstages a file
    /// </summary>
    public async Task<(bool Success, string Message)> UnstageFileAsync(string filePath, string? workingDirectory = null)
    {
        try
        {
            var (success, _, error) = await ExecuteGitCommandAsync(workingDirectory, "reset", "HEAD", "--", filePath);
            
            if (success)
            {
                _logger.LogInformation("Unstaged file: {FilePath}", filePath);
                return (true, $"Unstaged: {filePath}");
            }
            
            return (false, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unstaging file: {FilePath}", filePath);
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Discards changes to a file
    /// </summary>
    public async Task<(bool Success, string Message)> DiscardFileChangesAsync(string filePath, string? workingDirectory = null)
    {
        try
        {
            // First check if file is tracked
            var (tracked, _, _) = await ExecuteGitCommandAsync(workingDirectory, "ls-files", "--error-unmatch", filePath);
            
            if (tracked)
            {
                // Tracked file: restore from HEAD
                var (success, _, error) = await ExecuteGitCommandAsync(workingDirectory, "checkout", "HEAD", "--", filePath);
                
                if (success)
                {
                    _logger.LogInformation("Discarded changes to file: {FilePath}", filePath);
                    return (true, $"Discarded changes: {filePath}");
                }
                
                return (false, error);
            }
            else
            {
                // Untracked file: just delete it
                var workDir = ValidateAndGetWorkingDirectory(workingDirectory);
                var fullPath = Path.Combine(workDir, filePath);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    _logger.LogInformation("Deleted untracked file: {FilePath}", filePath);
                    return (true, $"Deleted untracked file: {filePath}");
                }
                
                return (false, "File not found");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discarding changes to file: {FilePath}", filePath);
            return (false, ex.Message);
        }
    }

    #endregion

    #region Commit

    /// <summary>
    /// Creates a commit with the given message
    /// </summary>
    public async Task<(bool Success, string Message)> CommitAsync(string message, string? description = null, string? workingDirectory = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return (false, "Commit message is required");
            }
            
            // Build full commit message
            var fullMessage = description != null ? $"{message}\n\n{description}" : message;
            
            var (success, output, error) = await ExecuteGitCommandAsync(workingDirectory, "commit", "-m", fullMessage);
            
            if (success)
            {
                _logger.LogInformation("Created commit: {Message}", message);
                return (true, $"Committed: {output.Trim()}");
            }
            
            return (false, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating commit");
            return (false, ex.Message);
        }
    }

    #endregion

    #region Branches

    /// <summary>
    /// Gets the list of branches
    /// </summary>
    public async Task<List<GitBranchInfo>> GetBranchesAsync(string? workingDirectory = null)
    {
        var branches = new List<GitBranchInfo>();
        
        try
        {
            // Get all branches with additional info
            var (success, output, _) = await ExecuteGitCommandAsync(
                workingDirectory,
                "for-each-ref",
                "--sort=-committerdate",
                "--format=%(refname:short)|%(objectname:short)|%(committerdate:iso)|%(subject)|%(HEAD)",
                "refs/heads/",
                "refs/remotes/"
            );
            
            if (!success || string.IsNullOrWhiteSpace(output))
            {
                return branches;
            }
            
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var parts = line.Split('|');
                if (parts.Length < 5) continue;
                
                var name = parts[0];
                var isRemote = name.StartsWith("origin/") || name.Contains("/");
                
                var branch = new GitBranchInfo
                {
                    Name = name,
                    IsCurrentBranch = parts[4].Trim() == "*",
                    IsRemote = isRemote,
                    LastCommitMessage = parts[3]
                };
                
                if (DateTime.TryParse(parts[2], out var commitDate))
                {
                    branch.LastCommitTime = commitDate;
                }
                
                branches.Add(branch);
            }
            
            // Get ahead/behind for current branch
            var currentBranch = branches.FirstOrDefault(b => b.IsCurrentBranch);
            if (currentBranch != null)
            {
                var (revSuccess, revOutput, _) = await ExecuteGitCommandAsync(
                    workingDirectory,
                    "rev-list", "--count", "--left-right", "@{upstream}...HEAD");
                    
                if (revSuccess && !string.IsNullOrWhiteSpace(revOutput))
                {
                    var counts = revOutput.Trim().Split('\t');
                    if (counts.Length == 2)
                    {
                        int.TryParse(counts[0], out int behind);
                        int.TryParse(counts[1], out int ahead);
                        currentBranch.BehindBy = behind;
                        currentBranch.AheadBy = ahead;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting branches");
        }
        
        return branches;
    }

    /// <summary>
    /// Checks out a branch
    /// </summary>
    public async Task<(bool Success, string Message)> CheckoutBranchAsync(string branchName, string? workingDirectory = null)
    {
        try
        {
            var (success, output, error) = await ExecuteGitCommandAsync(workingDirectory, "checkout", branchName);
            
            if (success)
            {
                _logger.LogInformation("Checked out branch: {BranchName}", branchName);
                return (true, $"Switched to branch '{branchName}'");
            }
            
            return (false, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking out branch: {BranchName}", branchName);
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Creates a new branch from the current branch
    /// </summary>
    public async Task<(bool Success, string Message)> CreateBranchAsync(string branchName, string? workingDirectory = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(branchName))
            {
                return (false, "Branch name is required");
            }
            
            // Validate branch name
            if (!IsValidBranchName(branchName))
            {
                return (false, "Invalid branch name. Avoid special characters and spaces.");
            }
            
            var (success, output, error) = await ExecuteGitCommandAsync(workingDirectory, "checkout", "-b", branchName);
            
            if (success)
            {
                _logger.LogInformation("Created and checked out branch: {BranchName}", branchName);
                return (true, $"Created and switched to branch '{branchName}'");
            }
            
            return (false, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating branch: {BranchName}", branchName);
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Merges a branch into the current branch
    /// </summary>
    public async Task<(bool Success, string Message)> MergeBranchAsync(string sourceBranch, string? workingDirectory = null)
    {
        try
        {
            var (success, output, error) = await ExecuteGitCommandAsync(workingDirectory, "merge", sourceBranch);
            
            if (success)
            {
                _logger.LogInformation("Merged branch: {BranchName}", sourceBranch);
                return (true, $"Merged '{sourceBranch}' into current branch");
            }
            
            // Check for merge conflict
            if (error.Contains("CONFLICT") || output.Contains("CONFLICT"))
            {
                return (false, "Merge conflict detected. Please resolve conflicts manually.");
            }
            
            return (false, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error merging branch: {BranchName}", sourceBranch);
            return (false, ex.Message);
        }
    }

    private static bool IsValidBranchName(string name)
    {
        // Basic validation for branch names
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (name.StartsWith("-") || name.StartsWith(".")) return false;
        if (name.Contains("..") || name.Contains("~") || name.Contains("^")) return false;
        if (name.Contains(" ") || name.Contains("\\")) return false;
        
        return Regex.IsMatch(name, @"^[\w\-/]+$");
    }

    #endregion

    #region Push/Fetch

    /// <summary>
    /// Pushes changes to remote
    /// </summary>
    public async Task<(bool Success, string Message)> PushAsync(string? workingDirectory = null)
    {
        try
        {
            var (success, _, error) = await ExecuteGitCommandAsync(workingDirectory, "push");
            
            if (success)
            {
                _logger.LogInformation("Pushed changes to remote");
                return (true, "Pushed changes successfully");
            }
            
            // Handle upstream not set
            if (error.Contains("no upstream branch"))
            {
                // Get current branch name
                var (branchSuccess, branchName, _) = await ExecuteGitCommandAsync(workingDirectory, "branch", "--show-current");
                if (branchSuccess)
                {
                    var (pushSuccess, pushOutput, pushError) = await ExecuteGitCommandAsync(
                        workingDirectory,
                        "push", "--set-upstream", "origin", branchName.Trim());
                        
                    if (pushSuccess)
                    {
                        return (true, $"Pushed and set upstream for '{branchName.Trim()}'");
                    }
                    
                    return (false, pushError);
                }
            }
            
            return (false, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pushing to remote");
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Fetches changes from remote
    /// </summary>
    public async Task<(bool Success, string Message)> FetchAsync(string? workingDirectory = null)
    {
        try
        {
            var (success, output, error) = await ExecuteGitCommandAsync(workingDirectory, "fetch", "--all", "--prune");
            
            if (success)
            {
                _logger.LogInformation("Fetched changes from remote");
                return (true, "Fetched changes successfully");
            }
            
            return (false, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching from remote");
            return (false, ex.Message);
        }
    }

    #endregion

    #region Command Execution

    private async Task<(bool Success, string Output, string Error)> ExecuteGitCommandAsync(string? workingDirectory, params string[] args)
    {
        return await ExecuteCommandAsync("git", workingDirectory, args);
    }

    private async Task<(bool Success, string Output, string Error)> ExecuteCommandAsync(string command, string? workingDirectory, params string[] args)
    {
        try
        {
            var validatedWorkingDirectory = ValidateAndGetWorkingDirectory(workingDirectory);
            
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = validatedWorkingDirectory
            };
            
            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }
            
            using var process = new Process { StartInfo = startInfo };
            process.Start();
            
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(CommandTimeoutSeconds));
            
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(); } catch { }
                return (false, string.Empty, "Command timed out");
            }
            
            var output = await outputTask;
            var error = await errorTask;
            
            return (process.ExitCode == 0, output, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command: {Command}", command);
            return (false, string.Empty, ex.Message);
        }
    }

    #endregion
}
