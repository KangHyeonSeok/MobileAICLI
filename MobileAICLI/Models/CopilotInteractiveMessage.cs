namespace MobileAICLI.Models;

/// <summary>
/// Represents a single message in an interactive Copilot conversation.
/// Used for client-side chat history and logging.
/// Phase 2: Interactive Mode - Issue 0
/// </summary>
public class CopilotInteractiveMessage
{
    /// <summary>
    /// Session ID this message belongs to
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Role of the message sender: "user" or "assistant"
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Content of the message
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the message was created
    /// </summary>
    public DateTime Timestamp { get; set; }
}
