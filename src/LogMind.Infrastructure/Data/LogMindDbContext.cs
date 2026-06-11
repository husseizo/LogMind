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
                Title = "Shopify Rate Limit Exceeded",
                Description = "Shopify API returns 429 Too Many Requests when rate limit is hit",
                ErrorPattern = "429|Too Many Requests|rate_limit_exceeded",
                Source = "Shopify",
                CreatedAt = new DateTime(2024, 1, 1),
                UpdatedAt = new DateTime(2024, 1, 1)
            },
            new KnownIssue
            {
                Id = 3,
                Title = "Finance DB Deadlock",
                Description = "Database deadlock detected in Finance transaction processing",
                ErrorPattern = "deadlock|transaction.*timeout|lock.*timeout",
                Source = "Finance",
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
                Title = "Implement Exponential Backoff",
                Steps = "1. Add retry logic with exponential backoff\n2. Respect the Retry-After header\n3. Limit to 4 requests per second per endpoint",
                References = "https://shopify.dev/docs/api/usage/rate-limits",
                Upvotes = 8,
                CreatedAt = new DateTime(2024, 1, 1)
            },
            new Solution
            {
                Id = 3,
                KnownIssueId = 3,
                Title = "Optimize Finance Transaction Queries",
                Steps = "1. Review long-running queries in Finance DB\n2. Add missing indexes on transaction tables\n3. Reduce transaction scope where possible\n4. Enable READ_COMMITTED_SNAPSHOT isolation",
                References = "Finance DB Runbook v2.3",
                Upvotes = 5,
                CreatedAt = new DateTime(2024, 1, 1)
            }
        );
    }
}
