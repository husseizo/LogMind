using LogMind.Core.Interfaces;
using LogMind.Core.Models;

namespace LogMind.Infrastructure.Services;

/// <summary>
/// Stub AI explanation service. Replace with a call to an LLM (e.g. Claude via Anthropic SDK).
/// </summary>
public class StubAiExplanationService : IAiExplanationService
{
    public Task<string> ExplainErrorAsync(LogEntry logEntry)
    {
        var explanation = $"Error in [{logEntry.Source}] at {logEntry.Timestamp:u}: {logEntry.Message}. " +
                          "AI explanation not yet configured — wire up an LLM provider in IAiExplanationService.";
        return Task.FromResult(explanation);
    }

    public Task<string> SuggestSolutionAsync(LogEntry logEntry, IEnumerable<KnownIssue> similarIssues)
    {
        var issues = similarIssues.ToList();
        if (issues.Count == 0)
            return Task.FromResult("No known issues matched. AI suggestion not yet configured.");

        var topIssue = issues[0];
        var topSolution = topIssue.Solutions.FirstOrDefault();
        var suggestion = topSolution != null
            ? $"Based on similar issue '{topIssue.Title}': {topSolution.Steps}"
            : $"Related known issue: {topIssue.Title} — {topIssue.Description}";

        return Task.FromResult(suggestion);
    }

    public Task<string> SummarizeTrendAsync(IEnumerable<LogEntry> entries)
    {
        var list = entries.ToList();
        var summary = $"Analyzed {list.Count} log entries. Top sources: " +
                      string.Join(", ", list.GroupBy(e => e.Source).OrderByDescending(g => g.Count()).Take(3).Select(g => $"{g.Key}({g.Count()})"));
        return Task.FromResult(summary);
    }

    public Task<string> ChatAsync(LogEntry logEntry, IEnumerable<(string Role, string Content)> history, string question)
        => Task.FromResult("AI chat not yet configured — wire up an LLM provider.");
}
