using LogMind.Core.Interfaces;
using LogMind.Core.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LogMind.Infrastructure.Data;

public class OperationalKnowledgeRepository : IOperationalKnowledgeRepository
{
    private readonly LogMindDbContext _db;

    public OperationalKnowledgeRepository(LogMindDbContext db) => _db = db;

    public async Task<List<OperationalKnowledge>> FindRelevantAsync(
        string logSource,
        float[]? queryVector = null,
        int topK = 2)
    {
        var all = await _db.OperationalKnowledge
            .Where(k => k.IsActive)
            .ToListAsync();

        // Filter to entries whose ApplicableSources contains this source
        var applicable = all.Where(k =>
        {
            var sources = JsonSerializer.Deserialize<List<string>>(k.ApplicableSources) ?? [];
            return sources.Any(s => s.Equals(logSource, StringComparison.OrdinalIgnoreCase));
        }).ToList();

        if (applicable.Count <= 1 || queryVector is null)
            return applicable.Take(topK).ToList();

        // Rank by cosine similarity when we have a query vector
        return applicable
            .Select(k =>
            {
                var score = 0f;
                if (k.EmbeddingVector is not null)
                {
                    var stored = JsonSerializer.Deserialize<float[]>(k.EmbeddingVector) ?? [];
                    score = CosineSimilarity(queryVector, stored);
                }
                return (Entry: k, Score: score);
            })
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => x.Entry)
            .ToList();
    }

    public Task<List<OperationalKnowledge>> GetAllAsync() =>
        _db.OperationalKnowledge.ToListAsync();

    public Task<OperationalKnowledge?> GetByIdAsync(int id) =>
        _db.OperationalKnowledge.FirstOrDefaultAsync(k => k.Id == id);

    public async Task UpdateEmbeddingAsync(int id, float[] vector)
    {
        var entry = await _db.OperationalKnowledge.FindAsync(id);
        if (entry is null) return;
        entry.EmbeddingVector = JsonSerializer.Serialize(vector);
        entry.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
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
