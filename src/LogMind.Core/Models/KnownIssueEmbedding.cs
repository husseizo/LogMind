namespace LogMind.Core.Models;

public class KnownIssueEmbedding
{
    public int Id { get; set; }
    public int KnownIssueId { get; set; }
    public KnownIssue KnownIssue { get; set; } = null!;
    // Stored as JSON float array
    public string VectorJson { get; set; } = "[]";
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
