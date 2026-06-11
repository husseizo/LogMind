using LogMind.Core.Models;

namespace LogMind.Core.Interfaces;

public interface IAiExplanationService
{
    Task<string> ExplainErrorAsync(LogEntry logEntry);
    Task<string> SuggestSolutionAsync(LogEntry logEntry, IEnumerable<KnownIssue> similarIssues);
    Task<string> SummarizeTrendAsync(IEnumerable<LogEntry> entries);
}
