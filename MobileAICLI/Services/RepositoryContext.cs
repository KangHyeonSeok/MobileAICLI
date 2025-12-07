using System.Diagnostics;
using Microsoft.Extensions.Options;
using MobileAICLI.Models;

namespace MobileAICLI.Services;

/// <summary>
/// Scoped service for managing current repository root and working directory.
/// Each user session has its own independent context.
/// </summary>
public class RepositoryContext
{
    private readonly MobileAICLISettings _settings;
    private readonly ILogger<RepositoryContext> _logger;
    private string _currentRoot;
    private string _currentWorkingPath;
    private readonly object _lock = new();

    public RepositoryContext(IOptions<MobileAICLISettings> settings, ILogger<RepositoryContext> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        
        // Initialize with default from settings or OS Documents directory
        _currentRoot = GetDefaultRepositoryPath();
        _currentWorkingPath = string.Empty;
        
        _logger.LogInformation("RepositoryContext initialized with root: {Root}", _currentRoot);
    }

    /// <summary>
    /// Gets the current repository root path
    /// </summary>
    public string CurrentRoot
    {
        get
        {
            lock (_lock)
            {
                return _currentRoot;
            }
        }
    }

    /// <summary>
    /// Gets the current working path (relative to root)
    /// </summary>
    public string CurrentWorkingPath
    {
        get
        {
            lock (_lock)
            {
                return _currentWorkingPath;
            }
        }
    }

    /// <summary>
    /// Gets the absolute path by combining current root and working path
    /// </summary>
    public string GetAbsolutePath(string? relativePath = null)
    {
        lock (_lock)
        {
            var basePath = string.IsNullOrEmpty(_currentWorkingPath)
                ? _currentRoot
                : Path.Combine(_currentRoot, _currentWorkingPath);

            if (string.IsNullOrEmpty(relativePath))
                return basePath;

            return Path.Combine(basePath, relativePath);
        }
    }

    /// <summary>
    /// Changes the repository root with validation
    /// </summary>
    public async Task<(bool Success, string Message)> ChangeRootAsync(string newRoot)
    {
        try
        {
            // Normalize path
            var normalizedRoot = Path.GetFullPath(newRoot);

            // Validate root
            var validation = await ValidateRootAsync(normalizedRoot);
            if (!validation.IsValid)
            {
                return (false, validation.ErrorMessage);
            }

            lock (_lock)
            {
                _currentRoot = normalizedRoot;
                _currentWorkingPath = string.Empty; // Reset working path when root changes
            }

            _logger.LogInformation("Repository root changed to: {Root}", normalizedRoot);
            return (true, "Repository root changed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing repository root to: {Root}", newRoot);
            return (false, $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Changes the current working path (relative to root)
    /// </summary>
    public (bool Success, string Message) ChangeWorkingPath(string relativePath)
    {
        try
        {
            string normalizedPath;
            
            lock (_lock)
            {
                // Combine with root and normalize
                var fullPath = Path.GetFullPath(Path.Combine(_currentRoot, relativePath));

                // Validate path is within root
                var relPath = Path.GetRelativePath(_currentRoot, fullPath);
                if (relPath.StartsWith("..") || Path.IsPathRooted(relPath))
                {
                    return (false, "Path is outside repository root");
                }

                // Check if directory exists
                if (!Directory.Exists(fullPath))
                {
                    return (false, "Directory does not exist");
                }

                // Check for symbolic links that escape root
                if (IsSymbolicLinkEscapingRoot(fullPath, _currentRoot))
                {
                    return (false, "Symbolic link escapes repository root");
                }

                normalizedPath = relPath;
                _currentWorkingPath = normalizedPath;
            }

            _logger.LogInformation("Working path changed to: {Path}", normalizedPath);
            return (true, "Working path changed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing working path to: {Path}", relativePath);
            return (false, $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates a path is within the current root
    /// </summary>
    public bool ValidatePathWithinRoot(string absolutePath)
    {
        lock (_lock)
        {
            var fullPath = Path.GetFullPath(absolutePath);
            var relPath = Path.GetRelativePath(_currentRoot, fullPath);
            
            return !relPath.StartsWith("..") && !Path.IsPathRooted(relPath);
        }
    }

    /// <summary>
    /// Validates a repository root path
    /// </summary>
    private async Task<(bool IsValid, string ErrorMessage)> ValidateRootAsync(string rootPath)
    {
        // Check if directory exists
        if (!Directory.Exists(rootPath))
        {
            return (false, "Directory does not exist");
        }

        // Check for symbolic links
        try
        {
            var dirInfo = new DirectoryInfo(rootPath);
            if (dirInfo.LinkTarget != null)
            {
                return (false, "Symbolic links are not allowed as root");
            }
        }
        catch
        {
            // If we can't check, be safe and reject
            return (false, "Unable to verify directory safety");
        }

        // Check if it's a Git repository
        var isGitRepo = await IsGitRepositoryAsync(rootPath);
        if (!isGitRepo)
        {
            return (false, "Not a Git repository");
        }

        // Check against whitelist if configured
        if (_settings.AllowedRepositoryRoots.Count > 0)
        {
            var isAllowed = _settings.AllowedRepositoryRoots.Any(pattern =>
            {
                // Simple pattern matching - exact match or wildcard
                if (pattern.EndsWith("*"))
                {
                    var prefix = pattern.TrimEnd('*');
                    return rootPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
                }
                return string.Equals(pattern, rootPath, StringComparison.OrdinalIgnoreCase);
            });

            if (!isAllowed)
            {
                return (false, "Repository root not in allowed list");
            }
        }

        return (true, string.Empty);
    }

    /// <summary>
    /// Checks if a path is a Git repository
    /// </summary>
    private async Task<bool> IsGitRepositoryAsync(string path)
    {
        // Check for .git directory
        if (Directory.Exists(Path.Combine(path, ".git")))
        {
            return true;
        }

        // Try git rev-parse command
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _settings.GitCliPath,
                Arguments = "rev-parse --is-inside-work-tree",
                WorkingDirectory = path,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process? process = null;
            try
            {
                process = Process.Start(startInfo);
                if (process == null)
                    return false;

                // Use timeout to prevent hanging
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                    return process.ExitCode == 0;
                }
                catch (OperationCanceledException)
                {
                    try 
                    { 
                        process.Kill(); 
                    } 
                    catch (InvalidOperationException)
                    {
                        // Process already exited
                    }
                    return false;
                }
            }
            finally
            {
                process?.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error checking Git repository status for: {Path}", path);
            return false;
        }
    }

    /// <summary>
    /// Checks if a symbolic link escapes the repository root
    /// </summary>
    private bool IsSymbolicLinkEscapingRoot(string path, string root)
    {
        try
        {
            var dirInfo = new DirectoryInfo(path);
            
            // Check if it's a symbolic link
            if (dirInfo.LinkTarget != null)
            {
                var targetPath = Path.GetFullPath(dirInfo.LinkTarget);
                var relPath = Path.GetRelativePath(root, targetPath);
                
                return relPath.StartsWith("..") || Path.IsPathRooted(relPath);
            }

            return false;
        }
        catch
        {
            // If we can't determine, assume it's unsafe
            return true;
        }
    }

    /// <summary>
    /// Gets the default repository path from settings or OS Documents directory
    /// </summary>
    private string GetDefaultRepositoryPath()
    {
        // First try configured path
        if (!string.IsNullOrWhiteSpace(_settings.RepositoryPath) && 
            Directory.Exists(_settings.RepositoryPath))
        {
            return Path.GetFullPath(_settings.RepositoryPath);
        }

        // Fall back to OS Documents directory
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        
        if (!string.IsNullOrEmpty(documentsPath) && Directory.Exists(documentsPath))
        {
            _logger.LogInformation("Using Documents directory as default: {Path}", documentsPath);
            return documentsPath;
        }

        // Last resort: current directory
        _logger.LogWarning("Using current directory as default repository path");
        return Environment.CurrentDirectory;
    }
}
