using LogMind.Core.Interfaces;
using LogMind.Core.Models;
using LogMind.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogMind.Infrastructure.Services;

public class KeywordSearchService : ISearchService
{
    private readonly LogMindDbContext _db;
    private readonly ILogRepository _logRepository;

    public KeywordSearchService(LogMindDbContext db, ILogRepository logRepository)
    {
        _db = db;
        _logRepository = logRepository;
    }

    public async Task<IEnumerable<KnownIssue>> FindSimilarIssuesAsync(string errorMessage, int topK = 5)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            return Enumerable.Empty<KnownIssue>();

        var keywords = ExtractKeywords(errorMessage);
        var issues = await _db.KnownIssues.Include(i => i.Solutions).ToListAsync();

        var scored = issues
            .Select(issue => new
            {
                Issue = issue,
                Score = ScoreIssue(issue, keywords, errorMessage)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => x.Issue);

        return scored;
    }

    public async Task<IEnumerable<LogEntry>> SearchLogsAsync(string query, string? source = null)
        => await _logRepository.SearchAsync(query, source);

    private static IEnumerable<string> ExtractKeywords(string text)
        => text.Split([' ', '\n', '\r', '\t', '.', ':', ';', ',', '(', ')', '[', ']'], StringSplitOptions.RemoveEmptyEntries)
               .Where(w => w.Length > 3)
               .Select(w => w.ToLowerInvariant())
               .Distinct();

    private static int ScoreIssue(KnownIssue issue, IEnumerable<string> keywords, string rawMessage)
    {
        var score = 0;
        var patternKeywords = issue.ErrorPattern.Split('|', StringSplitOptions.RemoveEmptyEntries);

        foreach (var pk in patternKeywords)
        {
            if (rawMessage.Contains(pk, StringComparison.OrdinalIgnoreCase))
                score += 3;
        }

        foreach (var kw in keywords)
        {
            if (issue.Title.Contains(kw, StringComparison.OrdinalIgnoreCase)) score += 1;
            if (issue.Description.Contains(kw, StringComparison.OrdinalIgnoreCase)) score += 1;
        }

        return score;
    }
}
