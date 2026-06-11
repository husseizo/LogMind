namespace LogMind.Core.Models;

public class Alert
{
    public int Id { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
    public int OccurrenceCount { get; set; }
    public int ThresholdCount { get; set; }
    public int WindowMinutes { get; set; }
    public string SampleMessage { get; set; } = string.Empty;
    public bool IsAcknowledged { get; set; }
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcknowledgedAt { get; set; }
}
