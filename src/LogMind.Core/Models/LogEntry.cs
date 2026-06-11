namespace LogMind.Core.Models;

public class LogEntry
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string? ErrorCode { get; set; }
    public string LogFile { get; set; } = string.Empty;
    public DateTime IngestedAt { get; set; } = DateTime.UtcNow;
}
