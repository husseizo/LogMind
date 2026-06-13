using System.Text.Json;
using LogMind.Core.Interfaces;
using LogMind.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace LogMind.Infrastructure.Services;

/// <summary>
/// Orchestrates the three-tier explanation cache cascade.
/// Controllers call GetOrExplainAsync instead of IAiExplanationService.ExplainErrorAsync directly.
/// </summary>
public class ExplanationCacheService
{
    // Bump this constant whenever the Ollama prompt format changes so cached entries from
    // old prompts can be filtered out via ExplanationCacheRepository queries.
    public const string CurrentPromptVersion = "v1";

    // Tier 2: token-Jaccard similarity threshold — must be >= this to count as a hit
    private const float StringSimilarityThreshold = 0.90f;

    // Tier 3: cosine similarity threshold — must be >= this to count as a hit
    private const float EmbeddingSimilarityThreshold = 0.85f;

    private readonly IExplanationCacheRepository _cache;
    private readonly IAiExplanationService _ollama;
    private readonly IEmbeddingService _embeddings;
    private readonly ISearchService _search;
    private readonly OllamaSettings _settings;
    private readonly ILogger<ExplanationCacheService> _logger;

    public ExplanationCacheService(
        IExplanationCacheRepository cache,
        IAiExplanationService ollama,
        IEmbeddingService embeddings,
        ISearchService search,
        OllamaSettings settings,
        ILogger<ExplanationCacheService> logger)
    {
        _cache      = cache;
        _ollama     = ollama;
        _embeddings = embeddings;
        _search     = search;
        _settings   = settings;
        _logger     = logger;
    }

    /// <summary>
    /// Invalidates the cached explanation for this log entry so the next explain call
    /// calls Ollama again. Called when the user marks an explanation as "not helpful".
    /// </summary>
    public async Task InvalidateExplanationAsync(LogEntry entry)
    {
        var normalized = MessageNormalizer.Normalize(entry.Message);
        var hash = MessageNormalizer.ComputeHash(entry.Source, normalized);
        await _cache.InvalidateByHashAsync(hash);
        _logger.LogInformation("Explanation invalidated by user feedback for {Source}/{Level}", entry.Source, entry.Level);
    }

    /// <summary>
    /// Returns an AI explanation for the given log entry, using the cache cascade:
    ///   Tier 1 (exact hash) → Tier 2 (string similarity) → Tier 3 (embedding) → Ollama.
    /// Only calls Ollama when all three tiers miss. Never writes to cache when Ollama is offline.
    /// </summary>
    public async Task<string> GetOrExplainAsync(LogEntry entry)
    {
        try
        {
            return await GetOrExplainCoreAsync(entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cache cascade failed for entry {Id} — falling back to direct Ollama call", entry.Id);
            return await _ollama.ExplainErrorAsync(entry);
        }
    }

    private async Task<string> GetOrExplainCoreAsync(LogEntry entry)
    {
        var normalized = MessageNormalizer.Normalize(entry.Message);
        var hash = MessageNormalizer.ComputeHash(entry.Source, normalized);

        // ── Tier 1: exact hash ────────────────────────────────────────────────
        var hit = await _cache.FindByHashAsync(hash);
        if (hit is not null)
        {
            _logger.LogDebug("Cache Tier1 hit for {Hash}", hash[..8]);
            await _cache.IncrementHitCountAsync(hit.Id);
            return hit.ExplanationJson;
        }

        // ── Tier 2: token-Jaccard string similarity ───────────────────────────
        var candidates = await _cache.GetBySourceAsync(entry.Source, limit: 200);
        var tier2Match = FindBestStringMatch(normalized, candidates);
        if (tier2Match is not null)
        {
            _logger.LogDebug("Cache Tier2 hit for source={Source}", entry.Source);
            await _cache.IncrementHitCountAsync(tier2Match.Id);
            return tier2Match.ExplanationJson;
        }

        // ── Tier 3: embedding cosine similarity ───────────────────────────────
        float[]? queryVector = null;
        if (_embeddings.IsAvailable)
        {
            try
            {
                queryVector = await _embeddings.GetEmbeddingAsync(normalized);
                var tier3Match = await _cache.FindSimilarByEmbeddingAsync(
                    queryVector, EmbeddingSimilarityThreshold);

                if (tier3Match is not null)
                {
                    _logger.LogDebug("Cache Tier3 hit for {Hash}", hash[..8]);
                    await _cache.IncrementHitCountAsync(tier3Match.Id);
                    return tier3Match.ExplanationJson;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Embedding service failed during Tier3 lookup; skipping to Ollama");
                queryVector = null;
            }
        }

        // ── Full miss: retrieve RAG context then call Ollama ─────────────────
        _logger.LogDebug("Cache miss — building RAG context for {Source}/{Level}", entry.Source, entry.Level);
        var context = await BuildExplainContextAsync(entry, candidates);
        _logger.LogDebug("RAG context: {Issues} known issues, {Logs} similar logs, {Prev} previous explanations",
            context.SimilarIssues.Count, context.SimilarLogs.Count, context.PreviousExplanations.Count);
        var explanation = await _ollama.ExplainErrorAsync(entry, context);

        if (!IsOllamaStub(explanation))
        {
            var vectorJson = queryVector is not null
                ? JsonSerializer.Serialize(queryVector)
                : null;

            try
            {
                await _cache.UpsertAsync(new AiExplanationCache
                {
                    LogEntryId        = entry.Id,
                    MessageHash       = hash,
                    NormalizedMessage = normalized,
                    Source            = entry.Source,
                    Level             = entry.Level,
                    Model             = _settings.Model,
                    PromptVersion     = CurrentPromptVersion,
                    ExplanationJson   = explanation,
                    EmbeddingVector   = vectorJson,
                    HitCount          = 0,
                    LastUsedAt        = DateTime.UtcNow,
                    CreatedAt         = DateTime.UtcNow,
                });
            }
            catch (Exception ex)
            {
                // Cache write failure must not prevent returning the explanation
                _logger.LogWarning(ex, "Failed to write explanation to cache for {Source}/{Level}", entry.Source, entry.Level);
            }
        }

        return explanation;
    }

    // ── RAG context builder ───────────────────────────────────────────────────

    private async Task<ExplainContext> BuildExplainContextAsync(
        LogEntry entry,
        IEnumerable<AiExplanationCache> tier2Candidates)
    {
        try
        {
            var issuesTask = _search.FindSimilarIssuesAsync(entry.Message, topK: 3);
            var logsTask   = _search.SearchLogsAsync(entry.Message, entry.Source);

            await Task.WhenAll(issuesTask, logsTask);

            var issues = (await issuesTask).ToList();

            var similarLogs = (await logsTask)
                .Where(l => l.Id != entry.Id && l.Level is "ERROR" or "FATAL" or "WARN")
                .Take(3)
                .ToList();

            // Reuse the Tier-2 candidates already in memory — no extra DB call.
            // These are non-invalidated previous explanations for the same source,
            // ordered by most recently used, giving the AI prior reasoning context.
            var prevExplanations = tier2Candidates
                .Where(c => !c.IsInvalidated)
                .OrderByDescending(c => c.LastUsedAt)
                .Take(3)
                .ToList();

            return new ExplainContext(issues, similarLogs, prevExplanations);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RAG context retrieval failed — proceeding with empty context");
            return ExplainContext.Empty;
        }
    }

    // ── Tier 2 helper ─────────────────────────────────────────────────────────

    private static AiExplanationCache? FindBestStringMatch(
        string normalized, IEnumerable<AiExplanationCache> candidates)
    {
        AiExplanationCache? bestMatch = null;
        var bestScore = StringSimilarityThreshold - 0.001f; // must beat threshold

        foreach (var c in candidates)
        {
            var score = TokenJaccard(normalized, c.NormalizedMessage);
            if (score > bestScore)
            {
                bestScore = score;
                bestMatch = c;
            }
        }

        return bestMatch;
    }

    /// <summary>
    /// Token-Jaccard similarity: split both strings into word tokens, compute
    /// |intersection| / |union|. Fast (O(n+m)) and reliable for log messages
    /// where our normalizer has already replaced dynamic values with placeholders.
    /// </summary>
    private static float TokenJaccard(string a, string b)
    {
        var setA = new HashSet<string>(
            a.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var setB = new HashSet<string>(
            b.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        var intersection = setA.Count(setB.Contains);
        var union = setA.Count + setB.Count - intersection;

        return union == 0 ? 0f : (float)intersection / union;
    }

    // ── Sentinel detection ────────────────────────────────────────────────────

    private static bool IsOllamaStub(string explanation) =>
        explanation.StartsWith("[Ollama offline]", StringComparison.OrdinalIgnoreCase) ||
        explanation.StartsWith("[Error]", StringComparison.OrdinalIgnoreCase) ||
        explanation.Equals("(no response from Ollama)", StringComparison.OrdinalIgnoreCase);
}
