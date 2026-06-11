using LogMind.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace LogMind.Infrastructure.Data;

public class LogMindDbContext : DbContext
{
    public LogMindDbContext(DbContextOptions<LogMindDbContext> options) : base(options) { }

    public DbSet<LogEntry> LogEntries => Set<LogEntry>();
    public DbSet<KnownIssue> KnownIssues => Set<KnownIssue>();
    public DbSet<Solution> Solutions => Set<Solution>();
    public DbSet<ErrorPattern> ErrorPatterns => Set<ErrorPattern>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<KnownIssueEmbedding> KnownIssueEmbeddings => Set<KnownIssueEmbedding>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LogEntry>(e =>
        {
            e.HasIndex(x => x.Timestamp);
            e.HasIndex(x => x.Source);
            e.HasIndex(x => x.Level);
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
