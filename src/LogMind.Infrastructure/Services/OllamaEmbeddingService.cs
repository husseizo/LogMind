using LogMind.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LogMind.Infrastructure.Services;

public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _http;
    private readonly ILogger<OllamaEmbeddingService> _logger;
    private readonly OllamaSettings _settings;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private bool _available = true;
    public bool IsAvailable => _available;

    public OllamaEmbeddingService(HttpClient http, OllamaSettings settings, ILogger<OllamaEmbeddingService> logger)
    {
        _http     = http;
        _settings = settings;
        _logger   = logger;
    }

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        var request = new EmbedRequest { Model = _settings.EmbeddingModel, Prompt = text };

        try
        {
            var response = await _http.PostAsJsonAsync("/api/embeddings", request, JsonOpts);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<EmbedResponse>(JsonOpts);
            _available = true;
            return result?.Embedding ?? [];
        }
        catch (HttpRequestException ex)
        {
            _available = false;
            _logger.LogWarning(ex, "Ollama embedding endpoint unavailable — falling back to keyword search");
            return [];
        }
    }

    private sealed class EmbedRequest
    {
        public required string Model  { get; init; }
        public required string Prompt { get; init; }
    }

    private sealed class EmbedResponse
    {
        public float[]? Embedding { get; init; }
    }
}
