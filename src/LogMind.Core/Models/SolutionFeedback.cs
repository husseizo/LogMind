namespace LogMind.Core.Models;

public class SolutionFeedback
{
    public int Id { get; set; }

    public int SolutionId { get; set; }
    public Solution Solution { get; set; } = null!;

    // The specific log entry the user applied the solution to — nullable
    public int? LogEntryId { get; set; }
    public LogEntry? LogEntry { get; set; }

    public bool Worked { get; set; }
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
