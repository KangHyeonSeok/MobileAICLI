using System.Collections.Generic;

namespace MobileAICLI.Models;

public class ToolProbeResult
{
    public List<string> CopilotPaths { get; set; } = new();
    public List<string> GhPaths { get; set; } = new();
    public List<string> GitPaths { get; set; } = new();
    public string? Error { get; set; }
}
