namespace InsuranceExtraction.Application.Models;

public class ChatRequest
{
    /// <summary>Full conversation history. Last entry is the new user message.</summary>
    public List<ChatMessage> Messages { get; set; } = new();
}

public class ChatMessage
{
    public string Role { get; set; } = string.Empty; // "user" | "assistant"
    public string Content { get; set; } = string.Empty;
}

public class ChatResponse
{
    public string Answer { get; set; } = string.Empty;
    public List<ToolCallRecord> ToolCalls { get; set; } = new();
}

public class ToolCallRecord
{
    public string ToolName { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
}
