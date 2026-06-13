using LogMind.Core.Models;

namespace LogMind.Core.Interfaces;

public interface IExplanationCacheRepository
{
    /// <summary>Tier 1 — exact hash lookup. Returns null if not found or invalidated.</summary>
    Task<AiExplanationCache?> FindByHashAsync(string hash);

    /// <summary>Tier 2 — fetch recent entries for the same source for string-similarity comparison.</summary>
    Task<List<AiExplanationCache>> GetBySourceAsync(string source, int limit = 200);

    /// <summary>
    /// Tier 3 — cosine similarity over stored embedding vectors.
    /// Returns the best match above <paramref name="threshold"/>, or null.
    /// </summary>
    Task<AiExplanationCache?> FindSimilarByEmbeddingAsync(float[] queryVector, float threshold = 0.85f);

    /// <summary>Insert a new cache entry or update the ExplanationJson/Model/PromptVersion if the hash already exists.</summary>
    Task UpsertAsync(AiExplanationCache entry);

    /// <summary>Increment HitCount and refresh LastUsedAt for the given cache entry.</summary>
    Task IncrementHitCountAsync(int id);

    /// <summary>Soft-delete: mark an entry as invalidated so it is bypassed on future lookups.</summary>
    Task InvalidateByHashAsync(string hash);
}
