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

    public Task<string> ExplainErrorAsync(LogEntry logEntry)
    {
        var isError = logEntry.Level is "ERROR" or "FATAL";
        var prompt = $"""
            You are a senior infrastructure engineer debugging a production system.
            Analyse this log entry and respond using EXACTLY this format — no deviation:

            **What happened:** (one sentence — the specific event or failure)
            **Component affected:** (exact class, service, module, or function extracted from the message or stack trace — never say "unknown")
            **Root cause:** (the most likely technical reason this occurred, be specific)
            **Fix steps:**
            1. (concrete action)
            2. (concrete action)
            3. (add more if needed)
            **What NOT to change:** (existing logic or infrastructure to preserve while fixing)

            Log entry:
            Source : {logEntry.Source}
            Level  : {logEntry.Level}
            Time   : {logEntry.Timestamp:u}
            Message: {logEntry.Message}
            {(logEntry.StackTrace is not null ? $"Stack trace:\n{logEntry.StackTrace}" : "")}

            {(isError
                ? "This is an ERROR/FATAL — be precise about what broke and how to fix it safely."
                : "This is informational — focus on what the event means and whether any action is needed.")}
            """;

        return OllamaChatAsync([new OllamaMessage { Role = "user", Content = prompt }]);
    }

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
