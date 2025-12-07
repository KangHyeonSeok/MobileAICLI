using Microsoft.Extensions.Options;
using MobileAICLI.Models;

namespace MobileAICLI.Services;

public class FileService
{
    private readonly MobileAICLISettings _settings;
    private readonly ILogger<FileService> _logger;

    public FileService(IOptionsSnapshot<MobileAICLISettings> settings, ILogger<FileService> logger)
    {
        _settings = settings.Value;
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
            var path = string.IsNullOrEmpty(relativePath) 
                ? _settings.RepositoryPath 
                : Path.Combine(_settings.RepositoryPath, relativePath);

            // Ensure path is within the repository with robust validation
            var fullPath = Path.GetFullPath(path);
            var repoPath = Path.GetFullPath(_settings.RepositoryPath);
            
            // Use Path.GetRelativePath to ensure path is within repository
            var relPath = Path.GetRelativePath(repoPath, fullPath);
            if (relPath.StartsWith("..") || Path.IsPathRooted(relPath))
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
                    FullPath = Path.GetRelativePath(_settings.RepositoryPath, dir),
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
                    FullPath = Path.GetRelativePath(_settings.RepositoryPath, file),
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
            var fullPath = Path.GetFullPath(Path.Combine(_settings.RepositoryPath, relativePath));
            var repoPath = Path.GetFullPath(_settings.RepositoryPath);

            // Use Path.GetRelativePath for robust path validation
            var relPath = Path.GetRelativePath(repoPath, fullPath);
            if (relPath.StartsWith("..") || Path.IsPathRooted(relPath))
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
            var fullPath = Path.GetFullPath(Path.Combine(_settings.RepositoryPath, relativePath));
            var repoPath = Path.GetFullPath(_settings.RepositoryPath);

            // Use Path.GetRelativePath for robust path validation
            var relPath = Path.GetRelativePath(repoPath, fullPath);
            if (relPath.StartsWith("..") || Path.IsPathRooted(relPath))
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
}
