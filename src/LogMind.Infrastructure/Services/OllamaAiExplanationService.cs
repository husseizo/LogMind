using LogMind.Core.Interfaces;
using LogMind.Core.Models;
using Microsoft.Extensions.Configuration;
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
        var prompt = $"""
            You are a senior infrastructure engineer. Analyse the following log error and give a concise explanation (3-5 sentences).
            Focus on: what went wrong, which system is affected, and the likely root cause.

            Source : {logEntry.Source}
            Level  : {logEntry.Level}
            Time   : {logEntry.Timestamp:u}
            Message: {logEntry.Message}
            {(logEntry.StackTrace is not null ? $"Stack trace:\n{logEntry.StackTrace}" : "")}
            """;

        return ChatAsync(prompt);
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

        return ChatAsync(prompt);
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

        return ChatAsync(prompt);
    }

    // ── Ollama HTTP ─────────────────────────────────────────────────────────

    private async Task<string> ChatAsync(string prompt)
    {
        var request = new OllamaChatRequest
        {
            Model  = _settings.Model,
            Stream = false,
            Messages = [new OllamaMessage { Role = "user", Content = prompt }]
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
            _logger.LogWarning(ex, "Ollama unavailable — falling back to stub explanation");
            return $"[Ollama offline] {FallbackExplanation(prompt)}";
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

    // Minimal inline fallback so the API never returns nothing even if Ollama is down
    private static string FallbackExplanation(string prompt)
    {
        if (prompt.Contains("ExplainError", StringComparison.OrdinalIgnoreCase))
            return "AI explanation unavailable — check Ollama is running on the configured host.";
        if (prompt.Contains("SuggestSolution", StringComparison.OrdinalIgnoreCase))
            return "AI suggestion unavailable — check Ollama is running on the configured host.";
        return "AI summary unavailable — check Ollama is running on the configured host.";
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
