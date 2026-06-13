using LogMind.Core.Models;

namespace LogMind.Core.Interfaces;

public interface IAiExplanationService
{
    Task<string> ExplainErrorAsync(LogEntry logEntry, ExplainContext? context = null);
    Task<string> SuggestSolutionAsync(LogEntry logEntry, IEnumerable<KnownIssue> similarIssues);
    Task<string> SummarizeTrendAsync(IEnumerable<LogEntry> entries);
    Task<string> ChatAsync(LogEntry logEntry, IEnumerable<(string Role, string Content)> history, string question);
}
