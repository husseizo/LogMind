using System.Text.RegularExpressions;
using LogMind.Core.Interfaces;
using LogMind.Core.Models;
using LogMind.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LogMind.Infrastructure.Services;

/// <summary>
/// Background service that groups ERROR/FATAL log entries into Incidents using time, source, dependency, and message rules.
/// Runs independently of AI — incidents exist before any AI Explain request is made.
/// Architecture: Logs arrive → CorrelationService → Incidents created → AI consumes incidents (Phase 3).
/// </summary>
public class IncidentCorrelationService : BackgroundService
{
    // ── Configuration ──────────────────────────────────────────────────────────
    private const int WindowMinutes      = 15;   // how far back to look for a matching open incident
    private const int ScanIntervalSecs   = 180;  // run every 3 minutes
    private const int StartupDelaySecs   = 25;   // let app finish migrating + seeding first
    private const float Threshold        = 70f;  // minimum score to join an existing incident
    private const int MaxEvents          = 100;  // cap events per incident before marking Investigating
    private const int AutoResolveMinutes = 30;   // close incidents with no new events after this

    // ── Scoring weights ────────────────────────────────────────────────────────
    private const float W_Time       = 20f;
    private const float W_SameSource = 10f;
    private const float W_Dependency = 30f;
    private const float W_Message    = 15f;
    // KnownIssue (+20) and Historical (+10) are Phase 2

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IncidentCorrelationService> _logger;

    public IncidentCorrelationService(
        IServiceScopeFactory scopeFactory,
        ILogger<IncidentCorrelationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(StartupDelaySecs), stoppingToken);
        _logger.LogInformation("IncidentCorrelationService started.");

        try { await RunBootstrapAsync(stoppingToken); }
        catch (Exception ex) { _logger.LogError(ex, "Bootstrap scan failed — real-time correlation will continue normally"); }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IncidentCorrelationService cycle failed — will retry next interval");
            }

            await Task.Delay(TimeSpan.FromSeconds(ScanIntervalSecs), stoppingToken);
        }
    }

    // ── One-time historical bootstrap ──────────────────────────────────────────

    private const int BootstrapDays    = 7;
    private const int BootstrapMaxLogs = 10_000;

    private async Task RunBootstrapAsync(CancellationToken stoppingToken)
    {
        using var scope      = _scopeFactory.CreateScope();
        var db               = scope.ServiceProvider.GetRequiredService<LogMindDbContext>();
        var incidentRepo     = scope.ServiceProvider.GetRequiredService<IIncidentRepository>();

        // Idempotency: skip if any incidents already exist
        if (await db.Incidents.AnyAsync(stoppingToken))
        {
            _logger.LogDebug("Bootstrap skipped — incidents already exist");
            return;
        }

        var since = DateTime.UtcNow.AddDays(-BootstrapDays);
        var historicalLogs = await db.LogEntries
            .Where(l => l.Timestamp >= since && (l.Level == "ERROR" || l.Level == "FATAL"))
            .OrderBy(l => l.Timestamp)
            .Take(BootstrapMaxLogs)
            .ToListAsync(stoppingToken);

        if (historicalLogs.Count == 0)
        {
            _logger.LogInformation("Bootstrap: no ERROR/FATAL logs found in last {Days} days — skipping", BootstrapDays);
            return;
        }

        _logger.LogInformation("Bootstrap: processing {Count} historical logs (last {Days} days, up to {Max})",
            historicalLogs.Count, BootstrapDays, BootstrapMaxLogs);

        var deps = await db.OperationalDependencies
            .Where(d => d.IsActive)
            .ToListAsync(stoppingToken);

        var candidates = new List<Incident>();
        int created = 0, joined = 0;

        foreach (var log in historicalLogs)
        {
            if (stoppingToken.IsCancellationRequested) break;

            // Prune candidates whose window has passed for this log — keeps the list small
            candidates.RemoveAll(c => (log.Timestamp - c.LastSeenAt).TotalMinutes > WindowMinutes + 1);

            var (bestIncident, bestScore, bestBasis) = FindBestCandidate(log, candidates, deps);

            if (bestIncident is not null && bestScore >= Threshold)
            {
                await EnrollInIncidentAsync(log, bestIncident, bestScore, bestBasis, incidentRepo);
                joined++;
            }
            else
            {
                var newIncident = await CreateIncidentAsync(log, incidentRepo);
                candidates.Add(newIncident);
                created++;
            }
        }

        // Auto-resolve: relative to the last log's timestamp, not UtcNow
        var lastTs         = historicalLogs.Max(l => l.Timestamp);
        var staleThreshold = lastTs.AddMinutes(-AutoResolveMinutes);
        var stale          = candidates
            .Where(c => c.Status == "Open" && c.LastSeenAt < staleThreshold)
            .ToList();

        foreach (var incident in stale)
        {
            incident.Status     = "Resolved";
            incident.ResolvedAt = incident.LastSeenAt.AddMinutes(AutoResolveMinutes);
            await incidentRepo.UpdateAsync(incident);
        }

        _logger.LogInformation(
            "Bootstrap complete — {Created} incidents created, {Joined} events joined, {Resolved} auto-resolved",
            created, joined, stale.Count);
    }

    // ── Main cycle ─────────────────────────────────────────────────────────────

    private async Task RunCycleAsync()
    {
        using var scope    = _scopeFactory.CreateScope();
        var db             = scope.ServiceProvider.GetRequiredService<LogMindDbContext>();
        var incidentRepo   = scope.ServiceProvider.GetRequiredService<IIncidentRepository>();

        var windowStart = DateTime.UtcNow.AddMinutes(-(WindowMinutes + ScanIntervalSecs / 60.0 + 2));

        // ── 1. Load unprocessed ERROR/FATAL logs from the correlation window ──
        var recentLogs = await db.LogEntries
            .Where(l => l.Timestamp >= windowStart
                     && (l.Level == "ERROR" || l.Level == "FATAL"))
            .OrderBy(l => l.Timestamp)
            .Take(500)
            .ToListAsync();

        if (recentLogs.Count == 0) return;

        // ── 2. Find which log IDs are already enrolled in an incident ─────────
        var processedIds = new HashSet<int>(
            await db.IncidentEvents
                .Where(e => e.CreatedAt >= windowStart)
                .Select(e => e.LogEntryId)
                .ToListAsync());

        var unprocessed = recentLogs.Where(l => !processedIds.Contains(l.Id)).ToList();
        if (unprocessed.Count == 0) return;

        _logger.LogDebug("Correlation cycle: {Total} recent error logs, {New} unprocessed",
            recentLogs.Count, unprocessed.Count);

        // ── 3. Load open incident candidates and dependency edges ─────────────
        var candidates = await incidentRepo.FindOpenCandidatesAsync(windowStart);
        var deps = await db.OperationalDependencies
            .Where(d => d.IsActive)
            .ToListAsync();

        // ── 4. Process each unprocessed log ───────────────────────────────────
        int created = 0, joined = 0;
        foreach (var log in unprocessed)
        {
            var (bestIncident, bestScore, bestBasis) = FindBestCandidate(log, candidates, deps);

            if (bestIncident is not null && bestScore >= Threshold)
            {
                await EnrollInIncidentAsync(log, bestIncident, bestScore, bestBasis, incidentRepo);
                joined++;
            }
            else
            {
                var newIncident = await CreateIncidentAsync(log, incidentRepo);
                candidates.Add(newIncident); // available immediately for subsequent logs this cycle
                created++;
            }
        }

        if (created > 0 || joined > 0)
            _logger.LogInformation("Correlation cycle complete — {Created} new incidents, {Joined} events joined existing",
                created, joined);

        // ── 5. Auto-resolve stale open incidents ─────────────────────────────
        await AutoResolveStaleAsync(candidates, incidentRepo);
    }

    // ── Scoring ────────────────────────────────────────────────────────────────

    private static (Incident? Best, float Score, List<string> Basis) FindBestCandidate(
        LogEntry log,
        List<Incident> candidates,
        List<OperationalDependency> deps)
    {
        Incident? best  = null;
        float bestScore = 0f;
        List<string> bestBasis = [];

        foreach (var candidate in candidates)
        {
            var (score, basis) = Score(log, candidate, deps);
            if (score > bestScore)
            {
                bestScore = score;
                best      = candidate;
                bestBasis = basis;
            }
        }

        return (best, bestScore, bestBasis);
    }

    private static (float Score, List<string> Basis) Score(
        LogEntry log,
        Incident candidate,
        List<OperationalDependency> deps)
    {
        float score      = 0f;
        var basis        = new List<string>();

        // Hard gate: log must be within [0, WindowMinutes] after incident's LastSeenAt
        var timeDelta = log.Timestamp - candidate.LastSeenAt;
        if (timeDelta.TotalMinutes < -1 || timeDelta.TotalMinutes > WindowMinutes)
            return (0f, basis);

        // Time proximity
        score += W_Time;
        basis.Add("TimeWindow");

        // Same source system
        if (string.Equals(log.Source, candidate.SourceSystem, StringComparison.OrdinalIgnoreCase))
        {
            score += W_SameSource;
            basis.Add("SameSource");
        }

        // Dependency chain: is log.Source a direct downstream of candidate.SourceSystem?
        var hasDep = deps.Any(d =>
            string.Equals(d.SourceSystem, candidate.SourceSystem, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(d.TargetSystem, log.Source, StringComparison.OrdinalIgnoreCase));

        if (hasDep)
        {
            score += W_Dependency;
            basis.Add("Dependency");
        }

        // Message similarity (Jaccard ≥ 0.40 — broader than cache tier, catches related failures)
        if (TokenJaccard(log.Message, candidate.RootLogMessage) >= 0.40f)
        {
            score += W_Message;
            basis.Add("SimilarMessage");
        }

        return (Math.Min(score, 100f), basis);
    }

    // ── Incident lifecycle ─────────────────────────────────────────────────────

    private static async Task EnrollInIncidentAsync(
        LogEntry log,
        Incident incident,
        float score,
        List<string> basis,
        IIncidentRepository repo)
    {
        var minutesFromRoot = (int)Math.Round((log.Timestamp - incident.FirstSeenAt).TotalMinutes);

        var evt = new IncidentEvent
        {
            IncidentId       = incident.Id,
            LogEntryId       = log.Id,
            CorrelationScore = score,
            Role             = DetermineRole(log, incident),
            CorrelationBasis = string.Join(",", basis),
            Sequence         = incident.EventCount,      // next slot (root is 0)
            MinutesFromRoot  = Math.Max(0, minutesFromRoot),
            CreatedAt        = DateTime.UtcNow,
        };

        await repo.AddEventAsync(evt);

        incident.EventCount++;
        incident.LastSeenAt = log.Timestamp > incident.LastSeenAt ? log.Timestamp : incident.LastSeenAt;

        // Escalate to Investigating when volume indicates a significant outage
        if (incident.EventCount >= MaxEvents && incident.Status == "Open")
            incident.Status = "Investigating";

        // Escalate severity if a FATAL event joins a High incident
        if (log.Level == "FATAL" && incident.Severity == "High")
            incident.Severity = "Critical";

        await repo.UpdateAsync(incident);
    }

    private static async Task<Incident> CreateIncidentAsync(LogEntry log, IIncidentRepository repo)
    {
        var incident = new Incident
        {
            Title               = GenerateTitle(log),
            IncidentFingerprint = GenerateFingerprint(log),
            RootCauseSummary    = $"Initial failure in {log.Source}: {Truncate(log.Message, 200)}",
            RootLogMessage      = log.Message,
            SourceSystem        = log.Source,
            Severity            = log.Level == "FATAL" ? "Critical" : "High",
            Status              = "Open",
            EventCount          = 1,
            FirstSeenAt         = log.Timestamp,
            LastSeenAt          = log.Timestamp,
            CreatedAt           = DateTime.UtcNow,
        };

        await repo.CreateAsync(incident);

        var rootEvent = new IncidentEvent
        {
            IncidentId       = incident.Id,
            LogEntryId       = log.Id,
            CorrelationScore = 100f,
            Role             = "RootCause",
            CorrelationBasis = "RootEvent",
            Sequence         = 0,
            MinutesFromRoot  = 0,
            CreatedAt        = DateTime.UtcNow,
        };

        await repo.AddEventAsync(rootEvent);
        return incident;
    }

    private static async Task AutoResolveStaleAsync(List<Incident> candidates, IIncidentRepository repo)
    {
        var staleThreshold = DateTime.UtcNow.AddMinutes(-AutoResolveMinutes);

        foreach (var incident in candidates.Where(i => i.Status == "Open" && i.LastSeenAt < staleThreshold))
        {
            incident.Status     = "Resolved";
            incident.ResolvedAt = DateTime.UtcNow;
            await repo.UpdateAsync(incident);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string DetermineRole(LogEntry log, Incident incident)
    {
        // FATAL on a non-critical incident → probable new root cause, not just downstream
        if (log.Level == "FATAL" && incident.Severity != "Critical") return "Symptom";

        // Source matches — same system, likely same failure manifesting differently
        if (string.Equals(log.Source, incident.SourceSystem, StringComparison.OrdinalIgnoreCase))
            return "Symptom";

        // Different source — downstream effect propagated by dependency chain
        return "DownstreamEffect";
    }

    private static string GenerateTitle(LogEntry log)
    {
        var msg = log.Message.Split('\n')[0];
        var truncated = msg.Length > 80 ? msg[..77] + "..." : msg;
        return $"[{log.Source}] {truncated}";
    }

    private static string GenerateFingerprint(LogEntry log)
    {
        var sourceSlug = Regex.Replace(log.Source.ToUpperInvariant(), @"[^A-Z0-9]+", "_").Trim('_');

        // Extract 3 meaningful words from the message (no numbers, no short words, no stop words)
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "the", "and", "for", "with", "from", "that", "this", "error", "failed", "failure",
              "exception", "could", "not", "null", "object", "value", "invalid", "unable" };

        var words = Regex.Split(log.Message, @"[^A-Za-z]+")
            .Where(w => w.Length > 3 && !Regex.IsMatch(w, @"^\d") && !stopWords.Contains(w))
            .Select(w => w.ToUpperInvariant())
            .Distinct()
            .Take(3)
            .ToArray();

        var fp = words.Length > 0
            ? $"{sourceSlug}_{string.Join("_", words)}"
            : sourceSlug;

        return fp.Length > 120 ? fp[..120] : fp;
    }

    private static float TokenJaccard(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return 0f;

        var setA = new HashSet<string>(
            a.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            StringComparer.OrdinalIgnoreCase);
        var setB = new HashSet<string>(
            b.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            StringComparer.OrdinalIgnoreCase);

        var intersection = setA.Count(setB.Contains);
        var union        = setA.Count + setB.Count - intersection;

        return union == 0 ? 0f : (float)intersection / union;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
