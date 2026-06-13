using LogMind.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace LogMind.Infrastructure.Data;

public class LogMindDbContext : DbContext
{
    public LogMindDbContext(DbContextOptions<LogMindDbContext> options) : base(options) { }

    public DbSet<LogEntry> LogEntries => Set<LogEntry>();
    public DbSet<KnownIssue> KnownIssues => Set<KnownIssue>();
    public DbSet<Solution> Solutions => Set<Solution>();
    public DbSet<SolutionFeedback> SolutionFeedback => Set<SolutionFeedback>();
    public DbSet<ErrorPattern> ErrorPatterns => Set<ErrorPattern>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<KnownIssueEmbedding> KnownIssueEmbeddings => Set<KnownIssueEmbedding>();
    public DbSet<AiExplanationCache> AiExplanationCache => Set<AiExplanationCache>();
    public DbSet<OperationalKnowledge> OperationalKnowledge => Set<OperationalKnowledge>();
    public DbSet<OperationalDependency> OperationalDependencies => Set<OperationalDependency>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<IncidentEvent> IncidentEvents => Set<IncidentEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LogEntry>(e =>
        {
            e.HasIndex(x => x.Timestamp);
            e.HasIndex(x => x.Source);
            e.HasIndex(x => x.Level);
            // Composite index for cursor-based pagination (DESC order mirrored in query)
            e.HasIndex(x => new { x.Timestamp, x.Id }).HasDatabaseName("ix_logs_timestamp_id");
            e.HasIndex(x => new { x.Source, x.Level }).HasDatabaseName("ix_logs_source_level");
        });

        modelBuilder.Entity<KnownIssue>()
            .HasMany(k => k.Solutions)
            .WithOne(s => s.KnownIssue)
            .HasForeignKey(s => s.KnownIssueId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ErrorPattern>()
            .HasOne(e => e.KnownIssue)
            .WithMany()
            .HasForeignKey(e => e.KnownIssueId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Alert>(e =>
        {
            e.HasIndex(x => x.TriggeredAt);
            e.HasIndex(x => x.IsAcknowledged);
            e.HasIndex(x => x.Source);
        });

        modelBuilder.Entity<KnownIssueEmbedding>()
            .HasOne(e => e.KnownIssue)
            .WithMany()
            .HasForeignKey(e => e.KnownIssueId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AiExplanationCache>(e =>
        {
            // Tier 1: exact hash lookup — must be unique per source+message combination
            e.HasIndex(x => x.MessageHash).IsUnique().HasDatabaseName("ix_cache_hash");
            // Tier 2: candidate fetch for string-similarity within same source
            e.HasIndex(x => new { x.Source, x.CreatedAt }).HasDatabaseName("ix_cache_source_createdat");
            // Cleanup job: find cold, non-invalidated entries by last use date
            e.HasIndex(x => new { x.IsInvalidated, x.LastUsedAt }).HasDatabaseName("ix_cache_invalidated_lastused");

            e.HasOne(x => x.LogEntry)
             .WithMany()
             .HasForeignKey(x => x.LogEntryId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(x => x.RelatedIssue)
             .WithMany()
             .HasForeignKey(x => x.RelatedIssueId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SolutionFeedback>(e =>
        {
            e.HasIndex(x => x.SolutionId).HasDatabaseName("ix_feedback_solutionid");
            e.HasIndex(x => x.LogEntryId).HasDatabaseName("ix_feedback_logentryid");

            e.HasOne(x => x.Solution)
             .WithMany(s => s.Feedback)
             .HasForeignKey(x => x.SolutionId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.LogEntry)
             .WithMany()
             .HasForeignKey(x => x.LogEntryId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<OperationalKnowledge>(e =>
        {
            e.HasIndex(x => x.IsActive).HasDatabaseName("ix_opknowledge_active");
            e.HasIndex(x => x.Category).HasDatabaseName("ix_opknowledge_category");
        });

        modelBuilder.Entity<OperationalDependency>(e =>
        {
            e.HasIndex(x => x.SourceSystem).HasDatabaseName("ix_opdep_source");
            e.HasIndex(x => x.IsActive).HasDatabaseName("ix_opdep_active");
        });

        modelBuilder.Entity<Incident>(e =>
        {
            e.HasIndex(x => new { x.SourceSystem, x.LastSeenAt }).HasDatabaseName("ix_incident_source_lastseen");
            e.HasIndex(x => x.Status).HasDatabaseName("ix_incident_status");
            e.HasIndex(x => x.IncidentFingerprint).HasDatabaseName("ix_incident_fingerprint");
            e.HasMany(x => x.Events)
             .WithOne(x => x.Incident)
             .HasForeignKey(x => x.IncidentId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IncidentEvent>(e =>
        {
            e.HasIndex(x => x.LogEntryId).HasDatabaseName("ix_incident_event_logentry");
            e.HasIndex(x => new { x.IncidentId, x.Sequence }).HasDatabaseName("ix_incident_event_sequence");
            e.HasOne(x => x.LogEntry)
             .WithMany()
             .HasForeignKey(x => x.LogEntryId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<KnownIssue>().HasData(
            new KnownIssue
            {
                Id = 1,
                Title = "SAP RFC Connection Failure",
                Description = "SAP RFC connection times out or is refused due to misconfigured gateway",
                ErrorPattern = "RFC_ERROR|CONNECT_FAILURE|SAPException",
                Source = "SAP",
                CreatedAt = new DateTime(2024, 1, 1),
                UpdatedAt = new DateTime(2024, 1, 1)
            },
            new KnownIssue
            {
                Id = 2,
                Title = "SapOdoo API Connection Timeout",
                Description = "Odoo API connection times out during SAP-Odoo synchronization",
                ErrorPattern = "ConnectionTimeout|timeout|ETIMEDOUT|Connection refused|ConnectError",
                Source = "SapOdoo",
                CreatedAt = new DateTime(2024, 1, 1),
                UpdatedAt = new DateTime(2024, 1, 1)
            },
            new KnownIssue
            {
                Id = 3,
                Title = "SapOdoo Sync Mapping Error",
                Description = "Data mapping error during SAP to Odoo sync, typically caused by missing or mismatched field mappings",
                ErrorPattern = "mapping.*error|IDOC|SyncException|sync_error|KeyError|field.*not found",
                Source = "SapOdoo",
                CreatedAt = new DateTime(2024, 1, 1),
                UpdatedAt = new DateTime(2024, 1, 1)
            }
        );

        modelBuilder.Entity<Solution>().HasData(
            new Solution
            {
                Id = 1,
                KnownIssueId = 1,
                Title = "Restart SAP Gateway Service",
                Steps = "1. Open SAP Management Console\n2. Navigate to SAP Gateway\n3. Stop and restart the gateway service\n4. Monitor connection logs for reconnection",
                References = "SAP Note 1234567",
                Upvotes = 12,
                CreatedAt = new DateTime(2024, 1, 1)
            },
            new Solution
            {
                Id = 2,
                KnownIssueId = 2,
                Title = "Check Odoo Server and Increase Timeout",
                Steps = "1. Check Odoo server is running and reachable\n2. Increase xmlrpc timeout in SapOdoo config\n3. Check firewall rules between SAP and Odoo servers\n4. Verify Odoo URL and credentials in appsettings",
                References = "Odoo Admin Guide — Timeouts",
                Upvotes = 6,
                CreatedAt = new DateTime(2024, 1, 1)
            },
            new Solution
            {
                Id = 3,
                KnownIssueId = 3,
                Title = "Fix SapOdoo Field Mapping Config",
                Steps = "1. Review the field mapping config in SapOdoo settings\n2. Compare SAP IDOC structure against Odoo model fields\n3. Add any missing field mappings and redeploy\n4. Re-run the failed sync batch from the SapOdoo admin panel",
                References = "SapOdoo Field Mapping Runbook",
                Upvotes = 4,
                CreatedAt = new DateTime(2024, 1, 1)
            }
        );
    }
}
