using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814

namespace LogMind.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KnownIssues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorPattern = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_KnownIssues", x => x.Id));

            migrationBuilder.CreateTable(
                name: "LogEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Level = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    StackTrace = table.Column<string>(type: "TEXT", nullable: true),
                    ErrorCode = table.Column<string>(type: "TEXT", nullable: true),
                    LogFile = table.Column<string>(type: "TEXT", nullable: false),
                    IngestedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_LogEntries", x => x.Id));

            migrationBuilder.CreateTable(
                name: "ErrorPatterns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Pattern = table.Column<string>(type: "TEXT", nullable: false),
                    PatternType = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: true),
                    KnownIssueId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErrorPatterns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ErrorPatterns_KnownIssues_KnownIssueId",
                        column: x => x.KnownIssueId,
                        principalTable: "KnownIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Solutions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KnownIssueId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Steps = table.Column<string>(type: "TEXT", nullable: false),
                    References = table.Column<string>(type: "TEXT", nullable: true),
                    Upvotes = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Solutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Solutions_KnownIssues_KnownIssueId",
                        column: x => x.KnownIssueId,
                        principalTable: "KnownIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "KnownIssues",
                columns: ["Id", "CreatedAt", "Description", "ErrorPattern", "Source", "Title", "UpdatedAt"],
                values: new object[,]
                {
                    { 1, new DateTime(2024, 1, 1), "SAP RFC connection times out or is refused due to misconfigured gateway", "RFC_ERROR|CONNECT_FAILURE|SAPException", "SAP", "SAP RFC Connection Failure", new DateTime(2024, 1, 1) },
                    { 2, new DateTime(2024, 1, 1), "Shopify API returns 429 Too Many Requests when rate limit is hit", "429|Too Many Requests|rate_limit_exceeded", "Shopify", "Shopify Rate Limit Exceeded", new DateTime(2024, 1, 1) },
                    { 3, new DateTime(2024, 1, 1), "Database deadlock detected in Finance transaction processing", "deadlock|transaction.*timeout|lock.*timeout", "Finance", "Finance DB Deadlock", new DateTime(2024, 1, 1) }
                });

            migrationBuilder.InsertData(
                table: "Solutions",
                columns: ["Id", "CreatedAt", "KnownIssueId", "References", "Steps", "Title", "Upvotes"],
                values: new object[,]
                {
                    { 1, new DateTime(2024, 1, 1), 1, "SAP Note 1234567", "1. Open SAP Management Console\n2. Navigate to SAP Gateway\n3. Stop and restart the gateway service\n4. Monitor connection logs for reconnection", "Restart SAP Gateway Service", 12 },
                    { 2, new DateTime(2024, 1, 1), 2, "https://shopify.dev/docs/api/usage/rate-limits", "1. Add retry logic with exponential backoff\n2. Respect the Retry-After header\n3. Limit to 4 requests per second per endpoint", "Implement Exponential Backoff", 8 },
                    { 3, new DateTime(2024, 1, 1), 3, "Finance DB Runbook v2.3", "1. Review long-running queries in Finance DB\n2. Add missing indexes on transaction tables\n3. Reduce transaction scope where possible\n4. Enable READ_COMMITTED_SNAPSHOT isolation", "Optimize Finance Transaction Queries", 5 }
                });

            migrationBuilder.CreateIndex(name: "IX_ErrorPatterns_KnownIssueId", table: "ErrorPatterns", column: "KnownIssueId");
            migrationBuilder.CreateIndex(name: "IX_LogEntries_Level", table: "LogEntries", column: "Level");
            migrationBuilder.CreateIndex(name: "IX_LogEntries_Source", table: "LogEntries", column: "Source");
            migrationBuilder.CreateIndex(name: "IX_LogEntries_Timestamp", table: "LogEntries", column: "Timestamp");
            migrationBuilder.CreateIndex(name: "IX_Solutions_KnownIssueId", table: "Solutions", column: "KnownIssueId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ErrorPatterns");
            migrationBuilder.DropTable(name: "Solutions");
            migrationBuilder.DropTable(name: "LogEntries");
            migrationBuilder.DropTable(name: "KnownIssues");
        }
    }
}
