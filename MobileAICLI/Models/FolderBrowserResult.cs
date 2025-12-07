namespace MobileAICLI.Models;

public class FolderBrowserResult
{
    public string CurrentPath { get; set; } = "";
    public string ParentPath { get; set; } = "";
    public List<FolderItem> Folders { get; set; } = new();
    public string? Error { get; set; }
    public bool Success => string.IsNullOrEmpty(Error);
}

public class FolderItem
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public bool IsAccessible { get; set; } = true;
}
