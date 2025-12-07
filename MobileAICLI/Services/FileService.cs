using Microsoft.Extensions.Options;
using MobileAICLI.Models;

namespace MobileAICLI.Services;

public class FileService
{
    private readonly RepositoryContext _context;
    private readonly ILogger<FileService> _logger;

    public FileService(RepositoryContext context, ILogger<FileService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public class FileItem
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
    }

    public async Task<List<FileItem>> GetFilesAsync(string? relativePath = null)
    {
        try
        {
            var fullPath = _context.GetAbsolutePath(relativePath);

            // Validate path is within root
            if (!_context.ValidatePathWithinRoot(fullPath))
            {
                _logger.LogWarning("Attempted to access path outside repository: {Path}", fullPath);
                return new List<FileItem>();
            }

            if (!Directory.Exists(fullPath))
            {
                return new List<FileItem>();
            }

            var items = new List<FileItem>();

            // Add directories
            foreach (var dir in Directory.GetDirectories(fullPath))
            {
                var dirInfo = new DirectoryInfo(dir);
                items.Add(new FileItem
                {
                    Name = dirInfo.Name,
                    FullPath = Path.GetRelativePath(_context.CurrentRoot, dir),
                    IsDirectory = true,
                    LastModified = dirInfo.LastWriteTime
                });
            }

            // Add files
            foreach (var file in Directory.GetFiles(fullPath))
            {
                var fileInfo = new FileInfo(file);
                items.Add(new FileItem
                {
                    Name = fileInfo.Name,
                    FullPath = Path.GetRelativePath(_context.CurrentRoot, file),
                    IsDirectory = false,
                    Size = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTime
                });
            }

            return items.OrderByDescending(i => i.IsDirectory).ThenBy(i => i.Name).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting files for path: {Path}", relativePath);
            return new List<FileItem>();
        }
    }

    public async Task<(bool Success, string Content)> ReadFileAsync(string relativePath)
    {
        try
        {
            var fullPath = _context.GetAbsolutePath(relativePath);

            // Validate path is within root
            if (!_context.ValidatePathWithinRoot(fullPath))
            {
                return (false, "Access denied: Path is outside repository");
            }

            if (!File.Exists(fullPath))
            {
                return (false, "File not found");
            }

            var content = await File.ReadAllTextAsync(fullPath);
            return (true, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading file: {Path}", relativePath);
            return (false, $"Error: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> WriteFileAsync(string relativePath, string content)
    {
        try
        {
            var fullPath = _context.GetAbsolutePath(relativePath);

            // Validate path is within root
            if (!_context.ValidatePathWithinRoot(fullPath))
            {
                return (false, "Access denied: Path is outside repository");
            }

            // Create directory if it doesn't exist
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(fullPath, content);
            return (true, "File saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing file: {Path}", relativePath);
            return (false, $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Browse directories for folder picker with security validation
    /// </summary>
    /// <param name="path">Target directory path. If null or empty, uses user's Documents folder</param>
    /// <returns>FolderBrowserResult containing current path, parent path, and list of accessible subdirectories</returns>
    /// <remarks>
    /// This method filters out hidden and system folders for security.
    /// Folders without access permission are marked as inaccessible.
    /// </remarks>
    public FolderBrowserResult BrowseDirectories(string? path = null)
    {
        try
        {
            var targetPath = string.IsNullOrWhiteSpace(path) 
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : path;

            // Resolve to full path and handle symlinks
            var fullPath = Path.GetFullPath(targetPath);

            // Check if path exists
            if (!Directory.Exists(fullPath))
            {
                return new FolderBrowserResult
                {
                    Error = "Directory does not exist"
                };
            }

            var result = new FolderBrowserResult
            {
                CurrentPath = fullPath
            };

            // Get parent directory if not at root
            try
            {
                var parent = Directory.GetParent(fullPath);
                if (parent != null)
                {
                    result.ParentPath = parent.FullName;
                }
            }
            catch
            {
                // Root directory has no parent
            }

            // Get subdirectories
            try
            {
                var directories = Directory.GetDirectories(fullPath);
                foreach (var dir in directories)
                {
                    try
                    {
                        var dirInfo = new DirectoryInfo(dir);
                        
                        // Skip hidden and system folders
                        if (dirInfo.Attributes.HasFlag(FileAttributes.Hidden) ||
                            dirInfo.Attributes.HasFlag(FileAttributes.System))
                        {
                            continue;
                        }

                        result.Folders.Add(new Models.FolderItem
                        {
                            Name = dirInfo.Name,
                            FullPath = dirInfo.FullName,
                            IsAccessible = true
                        });
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Skip folders we can't access
                        var dirName = Path.GetFileName(dir);
                        result.Folders.Add(new Models.FolderItem
                        {
                            Name = dirName,
                            FullPath = dir,
                            IsAccessible = false
                        });
                    }
                }

                // Sort folders by name
                result.Folders = result.Folders.OrderBy(f => f.Name).ToList();
            }
            catch (UnauthorizedAccessException)
            {
                return new FolderBrowserResult
                {
                    CurrentPath = fullPath,
                    Error = "Access denied to this directory"
                };
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error browsing directories: {Path}", path);
            return new FolderBrowserResult
            {
                Error = $"Error: {ex.Message}"
            };
        }
    }
}
