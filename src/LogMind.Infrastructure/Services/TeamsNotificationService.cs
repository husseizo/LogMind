using LogMind.Core.Interfaces;
using LogMind.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace LogMind.Infrastructure.Services;

public class TeamsNotificationService : INotificationService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<TeamsNotificationService> _logger;

    public TeamsNotificationService(HttpClient http, IConfiguration config, ILogger<TeamsNotificationService> logger)
    {
        _http   = http;
        _config = config;
        _logger = logger;
    }

    public async Task NotifyAlertAsync(Alert alert)
    {
        if (!_config.GetValue<bool>("Notifications:Teams:Enabled")) return;

        var url = _config["Notifications:Teams:WebhookUrl"];
        if (string.IsNullOrWhiteSpace(url)) return;

        var card = new
        {
            type    = "MessageCard",
            context = "http://schema.org/extensions",
            themeColor = "FF0000",
            summary = $"LogMind Alert: {alert.RuleName}",
            sections = new[]
            {
                new
                {
                    activityTitle    = $"\U0001f6a8 {alert.RuleName}",
                    activitySubtitle = $"[{alert.Source}] {alert.OccurrenceCount} errors in {alert.WindowMinutes} min",
                    facts = new[]
                    {
                        new { name = "Source",    value = alert.Source },
                        new { name = "Count",     value = $"{alert.OccurrenceCount} (threshold {alert.ThresholdCount})" },
                        new { name = "Triggered", value = alert.TriggeredAt.ToString("u") },
                        new { name = "Sample",    value = alert.SampleMessage }
                    },
                    markdown = true
                }
            }
        };

        try
        {
            var response = await _http.PostAsJsonAsync(url, card);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Teams webhook returned {Status} for rule '{Rule}'", response.StatusCode, alert.RuleName);
            else
                _logger.LogInformation("Teams alert sent for rule '{Rule}'", alert.RuleName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send Teams alert for rule '{Rule}'", alert.RuleName);
        }
    }
}
