using Microsoft.AspNetCore.SignalR;
using MobileAICLI.Models;
using MobileAICLI.Services;

namespace MobileAICLI.Hubs;

/// <summary>
/// SignalR Hub for Git operations
/// </summary>
public class GitHub : Hub
{
    private readonly GitService _gitService;
    private readonly ILogger<GitHub> _logger;

    public GitHub(GitService gitService, ILogger<GitHub> logger)
    {
        _gitService = gitService;
        _logger = logger;
    }

    #region Status

    /// <summary>
    /// Gets the current repository status
    /// </summary>
    public async Task<GitRepositoryStatus> GetStatus()
    {
        _logger.LogInformation("GetStatus called");
        return await _gitService.GetRepositoryStatusAsync();
    }

    /// <summary>
    /// Checks authentication status
    /// </summary>
    public async Task<(bool IsAuthenticated, string Message)> CheckAuthentication()
    {
        _logger.LogInformation("CheckAuthentication called");
        return await _gitService.CheckAuthenticationAsync();
    }

    #endregion

    #region Files

    /// <summary>
    /// Gets the list of changed files
    /// </summary>
    public async Task<List<GitFileChange>> GetChangedFiles()
    {
        _logger.LogInformation("GetChangedFiles called");
        return await _gitService.GetChangedFilesAsync();
    }

    /// <summary>
    /// Gets the diff for a specific file
    /// </summary>
    public async Task<GitDiffResult> GetFileDiff(string filePath)
    {
        _logger.LogInformation("GetFileDiff called for: {FilePath}", filePath);
        return await _gitService.GetFileDiffAsync(filePath);
    }

    /// <summary>
    /// Stages a file
    /// </summary>
    public async Task<(bool Success, string Message)> StageFile(string filePath)
    {
        _logger.LogInformation("StageFile called for: {FilePath}", filePath);
        var result = await _gitService.StageFileAsync(filePath);
        
        if (result.Success)
        {
            // Notify all clients about the change
            await Clients.Caller.SendAsync("FileStaged", filePath);
        }
        
        return result;
    }

    /// <summary>
    /// Unstages a file
    /// </summary>
    public async Task<(bool Success, string Message)> UnstageFile(string filePath)
    {
        _logger.LogInformation("UnstageFile called for: {FilePath}", filePath);
        var result = await _gitService.UnstageFileAsync(filePath);
        
        if (result.Success)
        {
            await Clients.Caller.SendAsync("FileUnstaged", filePath);
        }
        
        return result;
    }

    /// <summary>
    /// Discards changes to a file
    /// </summary>
    public async Task<(bool Success, string Message)> DiscardFileChanges(string filePath)
    {
        _logger.LogInformation("DiscardFileChanges called for: {FilePath}", filePath);
        var result = await _gitService.DiscardFileChangesAsync(filePath);
        
        if (result.Success)
        {
            await Clients.Caller.SendAsync("FileDiscarded", filePath);
        }
        
        return result;
    }

    #endregion

    #region Commit

    /// <summary>
    /// Creates a commit
    /// </summary>
    public async Task<(bool Success, string Message)> Commit(GitCommitRequest request)
    {
        _logger.LogInformation("Commit called with message: {Message}", TruncateForLog(request.Message));
        
        // Stage files if specified
        if (request.FilesToStage != null && request.FilesToStage.Count > 0)
        {
            foreach (var file in request.FilesToStage)
            {
                var stageResult = await _gitService.StageFileAsync(file);
                if (!stageResult.Success)
                {
                    return (false, $"Failed to stage {file}: {stageResult.Message}");
                }
            }
        }
        
        var result = await _gitService.CommitAsync(request.Message, request.Description);
        
        if (result.Success)
        {
            await Clients.Caller.SendAsync("CommitCreated", request.Message);
        }
        
        return result;
    }

    #endregion

    #region Branches

    /// <summary>
    /// Gets the list of branches
    /// </summary>
    public async Task<List<GitBranchInfo>> GetBranches()
    {
        _logger.LogInformation("GetBranches called");
        return await _gitService.GetBranchesAsync();
    }

    /// <summary>
    /// Checks out a branch
    /// </summary>
    public async Task<(bool Success, string Message)> CheckoutBranch(string branchName)
    {
        _logger.LogInformation("CheckoutBranch called for: {BranchName}", branchName);
        var result = await _gitService.CheckoutBranchAsync(branchName);
        
        if (result.Success)
        {
            await Clients.Caller.SendAsync("BranchChanged", branchName);
        }
        
        return result;
    }

    /// <summary>
    /// Creates a new branch
    /// </summary>
    public async Task<(bool Success, string Message)> CreateBranch(string branchName)
    {
        _logger.LogInformation("CreateBranch called for: {BranchName}", branchName);
        var result = await _gitService.CreateBranchAsync(branchName);
        
        if (result.Success)
        {
            await Clients.Caller.SendAsync("BranchCreated", branchName);
        }
        
        return result;
    }

    /// <summary>
    /// Merges a branch into the current branch
    /// </summary>
    public async Task<(bool Success, string Message)> MergeBranch(string sourceBranch)
    {
        _logger.LogInformation("MergeBranch called for: {SourceBranch}", sourceBranch);
        var result = await _gitService.MergeBranchAsync(sourceBranch);
        
        if (result.Success)
        {
            await Clients.Caller.SendAsync("BranchMerged", sourceBranch);
        }
        
        return result;
    }

    #endregion

    #region Push/Fetch

    /// <summary>
    /// Pushes changes to remote
    /// </summary>
    public async Task<(bool Success, string Message)> Push()
    {
        _logger.LogInformation("Push called");
        
        await Clients.Caller.SendAsync("OperationStarted", "push");
        
        var result = await _gitService.PushAsync();
        
        await Clients.Caller.SendAsync("OperationCompleted", "push", result.Success, result.Message);
        
        return result;
    }

    /// <summary>
    /// Fetches changes from remote
    /// </summary>
    public async Task<(bool Success, string Message)> Fetch()
    {
        _logger.LogInformation("Fetch called");
        
        await Clients.Caller.SendAsync("OperationStarted", "fetch");
        
        var result = await _gitService.FetchAsync();
        
        await Clients.Caller.SendAsync("OperationCompleted", "fetch", result.Success, result.Message);
        
        return result;
    }

    #endregion

    #region Connection Events

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Git client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Git client disconnected: {ConnectionId}, Exception: {Exception}",
            Context.ConnectionId, exception?.Message);
        await base.OnDisconnectedAsync(exception);
    }

    #endregion

    private static string TruncateForLog(string text, int maxLength = 100)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        return text[..maxLength] + "...";
    }
}
