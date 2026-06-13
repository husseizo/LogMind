namespace LogMind.Core.Models;

public class Incident
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Stable slug used for historical matching — e.g. MOLASLUBES_NEON_NEONINVOICE_SYNC_FAILURE.
    /// Derived from source + key message words, survives wording changes across incidents.
    /// </summary>
    public string IncidentFingerprint { get; set; } = string.Empty;

    public string RootCauseSummary { get; set; } = string.Empty;

    /// <summary>Stored denormalized so the correlation service can compare messages without joining LogEntries.</summary>
    public string RootLogMessage { get; set; } = string.Empty;

    /// <summary>Matches entry.Source from the root log entry.</summary>
    public string SourceSystem { get; set; } = string.Empty;

    /// <summary>Low | Medium | High | Critical</summary>
    public string Severity { get; set; } = "High";

    /// <summary>Open | Investigating | Resolved</summary>
    public string Status { get; set; } = "Open";

    /// <summary>Denormalized count — avoids COUNT(*) on list views.</summary>
    public int EventCount { get; set; } = 1;

    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<IncidentEvent> Events { get; set; } = [];
}
