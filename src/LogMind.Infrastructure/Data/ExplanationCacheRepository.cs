using System.Text.Json;
using LogMind.Core.Interfaces;
using LogMind.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace LogMind.Infrastructure.Data;

public class ExplanationCacheRepository : IExplanationCacheRepository
{
    private readonly LogMindDbContext _db;

    public ExplanationCacheRepository(LogMindDbContext db) => _db = db;

    // ── Tier 1 ───────────────────────────────────────────────────────────────

    public async Task<AiExplanationCache?> FindByHashAsync(string hash)
        => await _db.AiExplanationCache
            .FirstOrDefaultAsync(e => e.MessageHash == hash && !e.IsInvalidated);

    // ── Tier 2 ───────────────────────────────────────────────────────────────

    public async Task<List<AiExplanationCache>> GetBySourceAsync(string source, int limit = 200)
        => await _db.AiExplanationCache
            .Where(e => e.Source == source && !e.IsInvalidated)
            .OrderByDescending(e => e.LastUsedAt)
            .Take(limit)
            .ToListAsync();

    // ── Tier 3 ───────────────────────────────────────────────────────────────

    public async Task<AiExplanationCache?> FindSimilarByEmbeddingAsync(float[] queryVector, float threshold = 0.85f)
    {
        // Pull candidates that have a stored embedding — limit to avoid full-table scan.
        // Most-recently-used ordering means hot/repeat errors surface first.
        var candidates = await _db.AiExplanationCache
            .Where(e => e.EmbeddingVector != null && !e.IsInvalidated)
            .OrderByDescending(e => e.LastUsedAt)
            .Take(500)
            .ToListAsync();

        AiExplanationCache? bestMatch = null;
        var bestScore = threshold - 0.001f; // must beat threshold to qualify

        foreach (var candidate in candidates)
        {
            if (candidate.EmbeddingVector is null) continue;

            float[]? stored;
            try
            {
                stored = JsonSerializer.Deserialize<float[]>(candidate.EmbeddingVector);
            }
            catch
            {
                continue; // malformed vector — skip silently
            }

            if (stored is null || stored.Length != queryVector.Length) continue;

            var score = CosineSimilarity(queryVector, stored);
            if (score > bestScore)
            {
                bestScore = score;
                bestMatch = candidate;
            }
        }

        return bestMatch;
    }

    // ── Write operations ─────────────────────────────────────────────────────

    public async Task UpsertAsync(AiExplanationCache entry)
    {
        var existing = await _db.AiExplanationCache
            .FirstOrDefaultAsync(e => e.MessageHash == entry.MessageHash);

        if (existing is null)
        {
            await _db.AiExplanationCache.AddAsync(entry);
        }
        else
        {
            // Refresh explanation and metadata; preserve HitCount and original LogEntryId
            existing.ExplanationJson = entry.ExplanationJson;
            existing.Model = entry.Model;
            existing.PromptVersion = entry.PromptVersion;
            existing.EmbeddingVector = entry.EmbeddingVector;
            existing.IsInvalidated = false;
            existing.LastUsedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    public async Task IncrementHitCountAsync(int id)
    {
        var entry = await _db.AiExplanationCache.FindAsync(id);
        if (entry is null) return;

        entry.HitCount++;
        entry.LastUsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task InvalidateByHashAsync(string hash)
    {
        var entry = await _db.AiExplanationCache
            .FirstOrDefaultAsync(e => e.MessageHash == hash);
        if (entry is null) return;

        entry.IsInvalidated = true;
        await _db.SaveChangesAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static float CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0, magA = 0, magB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        var denom = Math.Sqrt(magA) * Math.Sqrt(magB);
        return denom < 1e-10 ? 0f : (float)(dot / denom);
    }
}
