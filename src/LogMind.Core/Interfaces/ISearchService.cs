using LogMind.Core.Models;

namespace LogMind.Core.Interfaces;

public interface ISearchService
{
    Task<IEnumerable<KnownIssue>> FindSimilarIssuesAsync(string errorMessage, int topK = 5);
    Task<IEnumerable<LogEntry>> SearchLogsAsync(string query, string? source = null);
}
