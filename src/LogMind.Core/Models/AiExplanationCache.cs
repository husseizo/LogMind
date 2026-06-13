namespace LogMind.Core.Models;

public class AiExplanationCache
{
    public int Id { get; set; }

    // The first log entry that triggered generation — nullable because log entries can be deleted
    public int? LogEntryId { get; set; }
    public LogEntry? LogEntry { get; set; }

    // Stable SHA-256 over "source::normalizedMessage" — unique index for Tier 1 lookup
    public string MessageHash { get; set; } = string.Empty;

    // Cleaned message used for Tier 2 string-similarity comparisons
    public string NormalizedMessage { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;

    // Bump this whenever the Ollama prompt format changes so old entries can be filtered out
    public string PromptVersion { get; set; } = "v1";

    public string ExplanationJson { get; set; } = string.Empty;

    // Serialized float[] (JSON) — stored so Tier 3 similarity search doesn't need to re-embed
    public string? EmbeddingVector { get; set; }

    public int HitCount { get; set; }
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Set to true when a solution was repeatedly marked "not worked" — bypassed on next lookup
    public bool IsInvalidated { get; set; }

    // The KnownIssue linked at generation time, if any
    public int? RelatedIssueId { get; set; }
    public KnownIssue? RelatedIssue { get; set; }
}
