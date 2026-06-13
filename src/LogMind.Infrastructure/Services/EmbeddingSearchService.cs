using LogMind.Core.Interfaces;
using LogMind.Core.Models;
using LogMind.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace LogMind.Infrastructure.Services;

/// <summary>
/// Semantic similarity search using Ollama nomic-embed-text embeddings stored in SQLite.
/// Falls back to KeywordSearchService when Ollama is unavailable.
/// </summary>
public class EmbeddingSearchService : ISearchService
{
    private readonly LogMindDbContext _db;
    private readonly ILogRepository _logRepository;
    private readonly IEmbeddingService _embedding;
    private readonly KeywordSearchService _keyword;
    private readonly ILogger<EmbeddingSearchService> _logger;

    public EmbeddingSearchService(
        LogMindDbContext db,
        ILogRepository logRepository,
        IEmbeddingService embedding,
        KeywordSearchService keyword,
        ILogger<EmbeddingSearchService> logger)
    {
        _db = db;
        _logRepository = logRepository;
        _embedding = embedding;
        _keyword = keyword;
        _logger = logger;
    }

    public async Task<IEnumerable<KnownIssue>> FindSimilarIssuesAsync(string errorMessage, int topK = 5)
    {
        // Try semantic search first
        if (_embedding.IsAvailable)
        {
            var queryVec = await _embedding.GetEmbeddingAsync(errorMessage);
            if (queryVec.Length > 0)
            {
                var results = await SemanticSearchAsync(queryVec, topK);
                if (results.Count > 0) return results;
            }
        }

        // Fallback to keyword
        _logger.LogDebug("Falling back to keyword search for: {Query}", errorMessage[..Math.Min(60, errorMessage.Length)]);
        return await _keyword.FindSimilarIssuesAsync(errorMessage, topK);
    }

    public async Task<IEnumerable<LogEntry>> SearchLogsAsync(string query, string? source = null)
        => await _logRepository.SearchAsync(query, source);

    // ── Called by EmbeddingIndexService on startup / when issues change ─────

    public async Task IndexKnownIssueAsync(KnownIssue issue)
    {
        var text = $"{issue.Title}. {issue.Description}. Pattern: {issue.ErrorPattern}";
        var vector = await _embedding.GetEmbeddingAsync(text);
        if (vector.Length == 0) return;

        var existing = await _db.KnownIssueEmbeddings.FirstOrDefaultAsync(e => e.KnownIssueId == issue.Id);
        if (existing is null)
        {
            await _db.KnownIssueEmbeddings.AddAsync(new KnownIssueEmbedding
            {
                KnownIssueId = issue.Id,
                VectorJson   = JsonSerializer.Serialize(vector),
                GeneratedAt  = DateTime.UtcNow
            });
        }
        else
        {
            existing.VectorJson   = JsonSerializer.Serialize(vector);
            existing.GeneratedAt  = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private async Task<List<KnownIssue>> SemanticSearchAsync(float[] queryVec, int topK)
    {
        var embeddings = await _db.KnownIssueEmbeddings
            .Include(e => e.KnownIssue)
                .ThenInclude(k => k.Solutions)
                    .ThenInclude(s => s.Feedback)
            .ToListAsync();

        return embeddings
            .Select(e =>
            {
                var stored = JsonSerializer.Deserialize<float[]>(e.VectorJson) ?? [];
                return new { e.KnownIssue, Score = CosineSimilarity(queryVec, stored) };
            })
            .Where(x => x.Score > 0.5f)
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => x.KnownIssue)
            .ToList();
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0 || a.Length != b.Length) return 0f;
        float dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot  += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }
        var denom = MathF.Sqrt(magA) * MathF.Sqrt(magB);
        return denom == 0 ? 0f : dot / denom;
    }
}
