namespace LogMind.Core.Interfaces;

public interface IEmbeddingService
{
    Task<float[]> GetEmbeddingAsync(string text);
    bool IsAvailable { get; }
}
