namespace LogMind.Core.Models;

public class IncidentEvent
{
    public int Id { get; set; }
    public int IncidentId { get; set; }
    public int LogEntryId { get; set; }

    public float CorrelationScore { get; set; }

    /// <summary>RootCause | DownstreamEffect | Symptom | Warning | Noise</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Comma-separated rules that fired: e.g. "TimeWindow,Dependency,SimilarMessage"</summary>
    public string CorrelationBasis { get; set; } = string.Empty;

    /// <summary>Temporal position within the incident — 0 = root event.</summary>
    public int Sequence { get; set; }

    /// <summary>Minutes elapsed since the root event — pre-computed for timeline rendering.</summary>
    public int MinutesFromRoot { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Incident Incident { get; set; } = null!;
    public LogEntry LogEntry { get; set; } = null!;
}
