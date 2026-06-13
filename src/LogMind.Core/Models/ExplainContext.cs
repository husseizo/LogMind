namespace LogMind.Core.Models;

/// <summary>
/// Operational context retrieved from LogMind's knowledge base before calling the AI.
/// Passed to ExplainErrorAsync so the model can reason over prior incidents and solutions.
/// </summary>
public record ExplainContext(
    IReadOnlyList<KnownIssue> SimilarIssues,
    IReadOnlyList<LogEntry> SimilarLogs,
    IReadOnlyList<AiExplanationCache> PreviousExplanations,
    IReadOnlyList<OperationalKnowledge> OperationalKnowledge,
    IReadOnlyList<OperationalDependency> DownstreamDependencies,
    Incident? RelatedIncident
)
{
    public static readonly ExplainContext Empty = new([], [], [], [], [], null);
}
