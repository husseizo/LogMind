namespace LogMind.Core.Models;

public class ErrorPattern
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
    public string PatternType { get; set; } = "Regex"; // Regex | Keyword
    public string Severity { get; set; } = "Error";
    public string? Source { get; set; }
    public int? KnownIssueId { get; set; }
    public KnownIssue? KnownIssue { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
