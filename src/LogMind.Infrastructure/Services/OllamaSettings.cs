using Microsoft.Extensions.Configuration;

namespace LogMind.Infrastructure.Services;

public class OllamaSettings
{
    private volatile string _model;
    private volatile string _embeddingModel;

    public OllamaSettings(IConfiguration config)
    {
        _model          = config["Ollama:Model"]          ?? "llama3";
        _embeddingModel = config["Ollama:EmbeddingModel"] ?? "nomic-embed-text";
    }

    public string Model          { get => _model;          set => _model = value; }
    public string EmbeddingModel { get => _embeddingModel; set => _embeddingModel = value; }
}
