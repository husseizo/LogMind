using LogMind.Core.Interfaces;
using LogMind.Core.Models;
using LogMind.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LogMind.Infrastructure.Services;

/// <summary>
/// Runs every minute, groups recent log errors by (Source, normalised message),
/// and fires an Alert when the count exceeds the configured threshold.
/// </summary>
public class AlertDetectionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<AlertDetectionService> _logger;

    // Tracks the last time a (source::pattern) combination fired an alert
    // so we don't spam duplicate alerts within the cool-down window.
    private readonly Dictionary<string, DateTime> _lastFired = new();

    public AlertDetectionService(IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<AlertDetectionService> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await DetectAsync(); }
            catch (Exception ex) { _logger.LogError(ex, "Alert detection cycle failed"); }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task DetectAsync()
    {
        var rules = _config.GetSection("AlertRules").GetChildren().ToList();
        if (rules.Count == 0) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LogMindDbContext>();

        foreach (var rule in rules)
        {
            var ruleName      = rule["Name"] ?? rule.Key;
            var source        = rule["Source"];             // null = all sources
            var pattern       = rule["Pattern"] ?? "";      // substring match
            var threshold     = rule.GetValue<int>("Threshold", 5);
            var windowMinutes = rule.GetValue<int>("WindowMinutes", 10);
            var cooldownMin   = rule.GetValue<int>("CooldownMinutes", 30);

            var since = DateTime.UtcNow.AddMinutes(-windowMinutes);

            var cooldownKey = $"{ruleName}::{source}::{pattern}";
            if (_lastFired.TryGetValue(cooldownKey, out var lastFire)
                && (DateTime.UtcNow - lastFire).TotalMinutes < cooldownMin)
                continue;

            var query = db.LogEntries
                .Where(e => e.Timestamp >= since && (e.Level == "ERROR" || e.Level == "FATAL"));

            if (!string.IsNullOrWhiteSpace(source))
                query = query.Where(e => e.Source == source);

            if (!string.IsNullOrWhiteSpace(pattern))
                query = query.Where(e => e.Message.Contains(pattern));

            // Group by source so one rule can cover multiple sources
            var groups = await query
                .GroupBy(e => e.Source)
                .Select(g => new { Source = g.Key, Count = g.Count(), Sample = g.OrderByDescending(e => e.Timestamp).First().Message })
                .Where(g => g.Count >= threshold)
                .ToListAsync();

            foreach (var group in groups)
            {
                var alert = new Alert
                {
                    RuleName         = ruleName,
                    Source           = group.Source,
                    Pattern          = pattern,
                    OccurrenceCount  = group.Count,
                    ThresholdCount   = threshold,
                    WindowMinutes    = windowMinutes,
                    SampleMessage    = group.Sample,
                    TriggeredAt      = DateTime.UtcNow
                };

                await db.Alerts.AddAsync(alert);
                _lastFired[cooldownKey] = DateTime.UtcNow;
                _logger.LogWarning("Alert fired: [{Rule}] {Source} — {Count} errors in {Window}min. Sample: {Sample}",
                    ruleName, group.Source, group.Count, windowMinutes, group.Sample);

                // Send notifications (fire-and-forget per notifier so one failure doesn't block others)
                var notifiers = scope.ServiceProvider.GetServices<INotificationService>();
                foreach (var notifier in notifiers)
                    _ = notifier.NotifyAlertAsync(alert).ContinueWith(t =>
                        _logger.LogWarning(t.Exception, "Notification failed"), TaskContinuationOptions.OnlyOnFaulted);
            }

            await db.SaveChangesAsync();
        }
    }
}
