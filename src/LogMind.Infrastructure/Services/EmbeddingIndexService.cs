using LogMind.Core.Interfaces;
using LogMind.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LogMind.Infrastructure.Services;

/// <summary>
/// On startup, generates embeddings for any KnownIssue that doesn't have one yet.
/// Re-runs every hour to pick up newly added issues.
/// </summary>
public class EmbeddingIndexService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmbeddingIndexService> _logger;

    public EmbeddingIndexService(IServiceScopeFactory scopeFactory, ILogger<EmbeddingIndexService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small delay to let the API finish starting up before hammering Ollama
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await IndexMissingAsync();
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task IndexMissingAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db        = scope.ServiceProvider.GetRequiredService<LogMindDbContext>();
        var search    = scope.ServiceProvider.GetRequiredService<EmbeddingSearchService>();
        var embedding = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
        var opRepo    = scope.ServiceProvider.GetRequiredService<IOperationalKnowledgeRepository>();

        // ── KnownIssues ──────────────────────────────────────────────────────
        var indexedIds = new HashSet<int>(await db.KnownIssueEmbeddings
            .Select(e => e.KnownIssueId)
            .ToListAsync());

        var unindexedIssues = await db.KnownIssues
            .Where(k => !indexedIds.Contains(k.Id))
            .ToListAsync();

        if (unindexedIssues.Count > 0)
        {
            _logger.LogInformation("Embedding {Count} new KnownIssues...", unindexedIssues.Count);
            foreach (var issue in unindexedIssues)
            {
                try { await search.IndexKnownIssueAsync(issue); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to embed issue {Id}: {Title}", issue.Id, issue.Title); }
            }
            _logger.LogInformation("KnownIssue embedding complete.");
        }

        // ── OperationalKnowledge ──────────────────────────────────────────────
        if (!embedding.IsAvailable) return;

        var allDocs = await opRepo.GetAllAsync();
        var unindexedDocs = allDocs.Where(d => d.IsActive && d.EmbeddingVector is null).ToList();

        if (unindexedDocs.Count > 0)
        {
            _logger.LogInformation("Embedding {Count} OperationalKnowledge document(s)...", unindexedDocs.Count);
            foreach (var doc in unindexedDocs)
            {
                try
                {
                    // Embed: title + category + tags + first 600 chars of content
                    var text = $"{doc.Title}. {doc.Category}. Tags: {doc.Tags}. {doc.Content[..Math.Min(600, doc.Content.Length)]}";
                    var vector = await embedding.GetEmbeddingAsync(text);
                    if (vector.Length > 0)
                        await opRepo.UpdateEmbeddingAsync(doc.Id, vector);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to embed OperationalKnowledge {Id}: {Title}", doc.Id, doc.Title);
                }
            }
            _logger.LogInformation("OperationalKnowledge embedding complete.");
        }
    }
}
