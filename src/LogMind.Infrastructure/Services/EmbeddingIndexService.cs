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
        var db      = scope.ServiceProvider.GetRequiredService<LogMindDbContext>();
        var search  = scope.ServiceProvider.GetRequiredService<EmbeddingSearchService>();

        var indexedIds = new HashSet<int>(await db.KnownIssueEmbeddings
            .Select(e => e.KnownIssueId)
            .ToListAsync());

        var unindexed = await db.KnownIssues
            .Where(k => !indexedIds.Contains(k.Id))
            .ToListAsync();

        if (unindexed.Count == 0) return;

        _logger.LogInformation("Embedding {Count} new KnownIssues...", unindexed.Count);
        foreach (var issue in unindexed)
        {
            try { await search.IndexKnownIssueAsync(issue); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to embed issue {Id}: {Title}", issue.Id, issue.Title); }
        }
        _logger.LogInformation("Embedding complete.");
    }
}
