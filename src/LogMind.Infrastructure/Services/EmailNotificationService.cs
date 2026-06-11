using LogMind.Core.Interfaces;
using LogMind.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;

namespace LogMind.Infrastructure.Services;

public class EmailNotificationService : INotificationService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(IConfiguration config, ILogger<EmailNotificationService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task NotifyAlertAsync(Alert alert)
    {
        if (!_config.GetValue<bool>("Notifications:Email:Enabled")) return;

        var host    = _config["Notifications:Email:SmtpHost"];
        var port    = _config.GetValue<int>("Notifications:Email:SmtpPort", 587);
        var useSsl  = _config.GetValue<bool>("Notifications:Email:UseSsl", true);
        var user    = _config["Notifications:Email:Username"] ?? "";
        var pass    = _config["Notifications:Email:Password"] ?? "";
        var from    = _config["Notifications:Email:From"] ?? "logmind@localhost";
        var toList  = _config.GetSection("Notifications:Email:To").Get<string[]>() ?? [];

        if (string.IsNullOrEmpty(host) || toList.Length == 0) return;

        try
        {
            using var smtp = new SmtpClient(host, port) { EnableSsl = useSsl };
            if (!string.IsNullOrEmpty(user))
                smtp.Credentials = new NetworkCredential(user, pass);

            var subject = $"[LogMind Alert] {alert.RuleName}";
            var body    = $"Rule   : {alert.RuleName}\n" +
                          $"Source : {alert.Source}\n" +
                          $"Count  : {alert.OccurrenceCount} errors in {alert.WindowMinutes} min (threshold {alert.ThresholdCount})\n" +
                          $"Time   : {alert.TriggeredAt:u}\n\n" +
                          $"Sample message:\n{alert.SampleMessage}";

            var msg = new MailMessage(from, toList[0], subject, body);
            foreach (var to in toList.Skip(1)) msg.To.Add(to);

            await smtp.SendMailAsync(msg);
            _logger.LogInformation("Alert email sent for rule '{Rule}'", alert.RuleName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send alert email for rule '{Rule}'", alert.RuleName);
        }
    }
}
