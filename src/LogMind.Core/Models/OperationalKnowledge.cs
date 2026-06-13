namespace LogMind.Core.Models;

/// <summary>
/// A structured knowledge document describing a business process, integration architecture,
/// or operational workflow. Retrieved semantically at explain time and injected into the
/// Ollama prompt so the AI can reason about business context, not just log text.
/// </summary>
public class OperationalKnowledge
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;       // e.g. "Business Process / Integration Architecture"
    public string System { get; set; } = string.Empty;         // e.g. "SAP + Odoo 19 + MolasLubes Cache + Neon Cache"
    public string Tags { get; set; } = string.Empty;           // comma-separated keywords
    public string Content { get; set; } = string.Empty;        // full knowledge text

    /// <summary>
    /// JSON array of exact Source names this knowledge applies to.
    /// Only retrieved when the log entry's Source matches one of these values.
    /// Example: ["SapOdoo Main", "Molaslubes Neon"]
    /// </summary>
    public string ApplicableSources { get; set; } = "[]";

    /// <summary>JSON-serialised float[] from nomic-embed-text. Null until indexed.</summary>
    public string? EmbeddingVector { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
