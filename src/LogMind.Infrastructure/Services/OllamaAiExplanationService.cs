using LogMind.Core.Interfaces;
using LogMind.Core.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LogMind.Infrastructure.Services;

public class OllamaAiExplanationService : IAiExplanationService
{
    private readonly HttpClient _http;
    private readonly ILogger<OllamaAiExplanationService> _logger;
    private readonly OllamaSettings _settings;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OllamaAiExplanationService(HttpClient http, OllamaSettings settings, ILogger<OllamaAiExplanationService> logger)
    {
        _http     = http;
        _settings = settings;
        _logger   = logger;
    }

    // ── Public interface ────────────────────────────────────────────────────

    public Task<string> ExplainErrorAsync(LogEntry logEntry, ExplainContext? context = null)
    {
        var ctx = context ?? ExplainContext.Empty;
        var retrievedBlock = BuildRetrievedContextBlock(ctx);

        var prompt = $"""
            You are LogMind AI — a senior software architect, operations engineer, systems analyst, and troubleshooting specialist embedded inside the LogMind platform.

            This environment runs: SAP Business One middleware, Odoo 19 integrations, ASP.NET Core 8 APIs, Python services, SQLite, Windows Server 2022, and background sync workers.

            Your purpose: diagnose failures accurately using the current log entry AND the retrieved operational context below. Always prioritize: (1) stack trace evidence, (2) exception message, (3) known issues, (4) solutions with positive feedback history.

            ── RETRIEVED OPERATIONAL CONTEXT ──────────────────────────────────────
            {retrievedBlock}
            ───────────────────────────────────────────────────────────────────────

            ── CURRENT LOG ENTRY ──────────────────────────────────────────────────
            Source  : {logEntry.Source}
            Level   : {logEntry.Level}
            Time    : {logEntry.Timestamp:u}
            Message : {logEntry.Message}
            {(logEntry.StackTrace is not null ? $"Stack trace:\n{logEntry.StackTrace}" : "")}
            ───────────────────────────────────────────────────────────────────────

            Respond using EXACTLY these section headers — no extras, no omissions:

            ## What Happened
            One paragraph describing the specific event or failure in plain English.

            ## Affected Component
            Identify the service, class, module, background worker, or API endpoint — extracted directly from the log.

            ## Retrieved Context Used
            State which of these you used: Known Issues | Previous Solutions | Similar historical logs | Feedback history. If none available: "No relevant retrieved context."

            ## Root Cause
            The most probable technical reason. If uncertain, state your assumptions explicitly.

            ## Recommended Solution
            Rank solutions by: (1) worked feedback history, (2) safety, (3) simplicity. If a Known Issue matches, reference it by title.

            ## Immediate Fix
            1. (concrete action)
            2. (concrete action)
            3. (add more if needed)

            ## Validation Steps
            How to confirm the fix worked and the system is healthy again.

            ## Prevention
            How to prevent this category of failure from recurring.

            ## Future Improvement
            One or two targeted recommendations for: code, architecture, monitoring, logging, retry, indexing, or alerting.

            ## Business / System Impact
            Impact on orders, invoices, customers, inventory, sync jobs, or reporting — if relevant. If none, say "No direct business impact."

            ## Confidence
            High / Medium / Low — and one sentence explaining why.
            """;

        return OllamaChatAsync([new OllamaMessage { Role = "user", Content = prompt }]);
    }

    // Score = WorkedCount*10 + Upvotes*3 - FailedCount*5
    private static int SolutionScore(Solution s)
    {
        var worked = s.Feedback.Count(f => f.Worked);
        var failed = s.Feedback.Count(f => !f.Worked);
        return worked * 10 + s.Upvotes * 3 - failed * 5;
    }

    private static string BuildRetrievedContextBlock(ExplainContext ctx)
    {
        var sb = new System.Text.StringBuilder();

        var hasContent = ctx.SimilarIssues.Count > 0
                      || ctx.SimilarLogs.Count > 0
                      || ctx.PreviousExplanations.Count > 0;

        if (!hasContent)
        {
            sb.AppendLine("No relevant context retrieved from the LogMind knowledge base.");
            return sb.ToString();
        }

        if (ctx.SimilarIssues.Count > 0)
        {
            sb.AppendLine("SIMILAR KNOWN ISSUES:");
            foreach (var issue in ctx.SimilarIssues.Take(3))
            {
                sb.AppendLine($"  Issue: \"{issue.Title}\" (Source: {issue.Source})");
                sb.AppendLine($"  Pattern: {issue.ErrorPattern}");
                if (!string.IsNullOrWhiteSpace(issue.Description))
                    sb.AppendLine($"  Description: {Truncate(issue.Description, 200)}");

                // Rank by composite score: WorkedCount*10 + Upvotes*3 - FailedCount*5
                var solutions = issue.Solutions.OrderByDescending(SolutionScore).Take(3).ToList();
                if (solutions.Count > 0)
                {
                    sb.AppendLine("  Solutions (ranked by worked feedback + upvotes − failures):");
                    foreach (var sol in solutions)
                    {
                        var worked = sol.Feedback.Count(f => f.Worked);
                        var failed = sol.Feedback.Count(f => !f.Worked);
                        var score  = SolutionScore(sol);
                        var stats  = sol.Feedback.Count > 0
                            ? $"score={score} | ✓ {worked} worked, ✗ {failed} failed, ↑ {sol.Upvotes} upvotes"
                            : $"↑ {sol.Upvotes} upvotes (no feedback yet)";
                        var reviewTag = sol.NeedsReview ? " [⚠ NEEDS REVIEW — solution reliability is in question]" : "";
                        sb.AppendLine($"    [{stats}{reviewTag}] \"{sol.Title}\"");
                        sb.AppendLine($"      Steps: {Truncate(sol.Steps, 200)}");
                        if (!string.IsNullOrWhiteSpace(sol.References))
                            sb.AppendLine($"      Ref: {sol.References}");
                    }
                }
                else
                {
                    sb.AppendLine("  Solutions: none recorded yet.");
                }
                sb.AppendLine();
            }
        }

        if (ctx.SimilarLogs.Count > 0)
        {
            sb.AppendLine("SIMILAR HISTORICAL LOG ENTRIES:");
            foreach (var log in ctx.SimilarLogs.Where(l => l.Level is "ERROR" or "FATAL" or "WARN").Take(3))
            {
                sb.AppendLine($"  [{log.Level}] {log.Timestamp:u} | {log.Source}");
                sb.AppendLine($"  {Truncate(log.Message, 180)}");
                if (log.StackTrace is not null)
                    sb.AppendLine($"  Trace: {Truncate(log.StackTrace, 120)}");
                sb.AppendLine();
            }
        }

        if (ctx.PreviousExplanations.Count > 0)
        {
            sb.AppendLine("PREVIOUS AI EXPLANATIONS (same source, similar messages — for pattern recognition):");
            foreach (var prev in ctx.PreviousExplanations)
            {
                sb.AppendLine($"  [{prev.Level}] cached {prev.LastUsedAt:u} | used {prev.HitCount}x");
                sb.AppendLine($"  Message context: {Truncate(prev.NormalizedMessage, 160)}");
                sb.AppendLine($"  Prior explanation summary: {Truncate(prev.ExplanationJson, 300)}");
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    public async Task<string> ChatAsync(LogEntry logEntry, IEnumerable<(string Role, string Content)> history, string question)
    {
        var systemContent = $"""
            You are a senior infrastructure engineer helping to debug and fix a specific production log entry.
            Keep this log entry as your reference for the entire conversation.

            Source : {logEntry.Source}
            Level  : {logEntry.Level}
            Time   : {logEntry.Timestamp:u}
            Message: {logEntry.Message}
            {(logEntry.StackTrace is not null ? $"Stack trace:\n{logEntry.StackTrace}" : "")}

            Rules:
            - Always answer in the context of this specific log entry and source system ({logEntry.Source}).
            - When suggesting fixes, be concrete and specific — name the exact function, config key, or code path.
            - Never suggest changes that could break existing logic or infrastructure unrelated to this issue.
            - If you need more info to answer precisely, ask the user for it.
            """;

        var messages = new List<OllamaMessage> { new() { Role = "system", Content = systemContent } };

        foreach (var (role, content) in history)
            messages.Add(new OllamaMessage { Role = role, Content = content });

        messages.Add(new OllamaMessage { Role = "user", Content = question });

        return await OllamaChatAsync(messages);
    }

    public Task<string> SuggestSolutionAsync(LogEntry logEntry, IEnumerable<KnownIssue> similarIssues)
    {
        var issueBlock = BuildIssueBlock(similarIssues);

        var prompt = $"""
            You are a senior infrastructure engineer. Suggest a fix for the error below.
            {(issueBlock.Length > 0 ? $"\nKnown similar issues from our knowledge base:\n{issueBlock}\n" : "")}
            Error details:
            Source : {logEntry.Source}
            Level  : {logEntry.Level}
            Time   : {logEntry.Timestamp:u}
            Message: {logEntry.Message}
            {(logEntry.StackTrace is not null ? $"Stack trace:\n{logEntry.StackTrace}" : "")}

            Respond with:
            1. Most likely cause (1-2 sentences)
            2. Recommended fix steps (numbered list)
            3. How to prevent recurrence (1-2 sentences)
            """;

        return OllamaChatAsync([new OllamaMessage { Role = "user", Content = prompt }]);
    }

    public Task<string> SummarizeTrendAsync(IEnumerable<LogEntry> entries)
    {
        var list = entries.ToList();
        var sample = list
            .Where(e => e.Level is "ERROR" or "FATAL")
            .OrderByDescending(e => e.Timestamp)
            .Take(20)
            .Select(e => $"[{e.Source}] {e.Message}");

        var prompt = $"""
            You are a senior infrastructure engineer reviewing a log trend report.
            Analyse the following {list.Count} log entries (showing top 20 errors) and provide:
            1. A 2-sentence summary of what is happening overall
            2. The top 3 recurring issues by source
            3. Any patterns or correlations worth investigating

            Log sample:
            {string.Join("\n", sample)}
            """;

        return OllamaChatAsync([new OllamaMessage { Role = "user", Content = prompt }]);
    }

    // ── Ollama HTTP ─────────────────────────────────────────────────────────

    private async Task<string> OllamaChatAsync(List<OllamaMessage> messages)
    {
        var request = new OllamaChatRequest
        {
            Model    = _settings.Model,
            Stream   = false,
            Messages = messages
        };

        try
        {
            var response = await _http.PostAsJsonAsync("/api/chat", request, JsonOpts);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOpts);
            return result?.Message?.Content?.Trim() ?? "(no response from Ollama)";
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Ollama unavailable");
            return "[Ollama offline] AI explanation unavailable — check Ollama is running on the configured host.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling Ollama");
            return $"[Error] Could not generate AI explanation: {ex.Message}";
        }
    }

    private static string BuildIssueBlock(IEnumerable<KnownIssue> issues)
    {
        var lines = issues.Select(i =>
            $"- {i.Title} ({i.Source}): {i.Description}" +
            (i.Solutions.FirstOrDefault() is { } s ? $"\n  Fix: {s.Steps.Split('\n')[0]}" : ""));
        return string.Join("\n", lines);
    }

    // ── DTOs ────────────────────────────────────────────────────────────────

    private sealed class OllamaChatRequest
    {
        public required string Model { get; init; }
        public bool Stream { get; init; }
        public required List<OllamaMessage> Messages { get; init; }
    }

    private sealed class OllamaMessage
    {
        public required string Role    { get; init; }
        public required string Content { get; init; }
    }

    private sealed class OllamaChatResponse
    {
        public OllamaMessage? Message { get; init; }
    }
}
