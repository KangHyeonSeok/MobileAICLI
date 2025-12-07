using System.Collections.Concurrent;

namespace MobileAICLI.Services;

public class AuditLogService
{
    private readonly ILogger<AuditLogService> _logger;
    private readonly ConcurrentQueue<AuditLogEntry> _logEntries = new();
    private const int MaxLogEntries = 1000;

    public AuditLogService(ILogger<AuditLogService> logger)
    {
        _logger = logger;
    }

    public void LogSettingsChange(string userName, string action, string details)
    {
        var entry = new AuditLogEntry
        {
            Timestamp = DateTime.UtcNow,
            UserName = userName,
            Action = action,
            Details = details,
            Category = "Settings"
        };

        _logEntries.Enqueue(entry);
        
        // Keep only the last MaxLogEntries
        while (_logEntries.Count > MaxLogEntries)
        {
            _logEntries.TryDequeue(out _);
        }

        _logger.LogInformation("Audit Log: {Category} - {Action} by {User} - {Details}", 
            entry.Category, entry.Action, entry.UserName, entry.Details);
    }

    public void LogPasswordChange(string userName, bool success)
    {
        var entry = new AuditLogEntry
        {
            Timestamp = DateTime.UtcNow,
            UserName = userName,
            Action = "PasswordChange",
            Details = success ? "Success" : "Failed",
            Category = "Security"
        };

        _logEntries.Enqueue(entry);
        
        while (_logEntries.Count > MaxLogEntries)
        {
            _logEntries.TryDequeue(out _);
        }

        _logger.LogWarning("Audit Log: {Category} - {Action} by {User} - {Details}", 
            entry.Category, entry.Action, entry.UserName, entry.Details);
    }

    public List<AuditLogEntry> GetRecentLogs(int count = 100)
    {
        return _logEntries.TakeLast(Math.Min(count, MaxLogEntries)).ToList();
    }

    public class AuditLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string UserName { get; set; } = "";
        public string Action { get; set; } = "";
        public string Details { get; set; } = "";
        public string Category { get; set; } = "";
    }
}
