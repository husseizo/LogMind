using LogMind.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace LogMind.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OllamaController : ControllerBase
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private readonly OllamaSettings _settings;

    public OllamaController(IHttpClientFactory factory, IConfiguration config, OllamaSettings settings)
    {
        _factory  = factory;
        _config   = config;
        _settings = settings;
    }

    /// <summary>Returns available models from the local Ollama instance.</summary>
    [HttpGet("models")]
    public async Task<IActionResult> GetModels()
    {
        var baseUrl = _config["Ollama:BaseUrl"] ?? "http://localhost:11434";
        var http = _factory.CreateClient();
        http.BaseAddress = new Uri(baseUrl);
        http.Timeout = TimeSpan.FromSeconds(5);

        try
        {
            var json = await http.GetStringAsync("/api/tags");
            using var doc = JsonDocument.Parse(json);
            var models = doc.RootElement
                .GetProperty("models")
                .EnumerateArray()
                .Select(m => m.GetProperty("name").GetString())
                .ToList();
            return Ok(new { baseUrl, models });
        }
        catch (Exception ex)
        {
            return Ok(new { baseUrl, models = Array.Empty<string>(), error = ex.Message });
        }
    }

    /// <summary>Returns the currently active Ollama settings (live, reflects hot-swaps).</summary>
    [HttpGet("config")]
    public IActionResult GetConfig() => Ok(new
    {
        baseUrl        = _config["Ollama:BaseUrl"] ?? "http://localhost:11434",
        model          = _settings.Model,
        embeddingModel = _settings.EmbeddingModel,
        timeoutSeconds = _config.GetValue<int>("Ollama:TimeoutSeconds", 120)
    });

    /// <summary>Hot-swap the chat/explanation model. Takes effect immediately — no restart needed.</summary>
    [HttpPut("model")]
    public IActionResult SetModel([FromBody] ModelUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Model))
            return BadRequest(new { error = "Model name is required." });
        _settings.Model = dto.Model.Trim();
        return Ok(new { model = _settings.Model });
    }

    /// <summary>Hot-swap the embedding model. Takes effect immediately — no restart needed.</summary>
    [HttpPut("embedding-model")]
    public IActionResult SetEmbeddingModel([FromBody] ModelUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Model))
            return BadRequest(new { error = "Model name is required." });
        _settings.EmbeddingModel = dto.Model.Trim();
        return Ok(new { embeddingModel = _settings.EmbeddingModel });
    }
}

public record ModelUpdateDto(string Model);
