using LogMind.Core.Models;

namespace LogMind.Core.Interfaces;

public interface IOperationalKnowledgeRepository
{
    /// <summary>
    /// Returns active knowledge entries whose ApplicableSources contains logSource,
    /// ranked by cosine similarity to queryVector when available.
    /// </summary>
    Task<List<OperationalKnowledge>> FindRelevantAsync(string logSource, float[]? queryVector = null, int topK = 2);

    Task<List<OperationalKnowledge>> GetAllAsync();
    Task<OperationalKnowledge?> GetByIdAsync(int id);
    Task UpdateEmbeddingAsync(int id, float[] vector);
}
