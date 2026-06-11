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
                    { 2, new DateTime(2024, 1, 1), "Odoo API connection times out during SAP-Odoo synchronization", "ConnectionTimeout|timeout|ETIMEDOUT|Connection refused|ConnectError", "SapOdoo", "SapOdoo API Connection Timeout", new DateTime(2024, 1, 1) },
                    { 3, new DateTime(2024, 1, 1), "Data mapping error during SAP to Odoo sync, typically caused by missing or mismatched field mappings", "mapping.*error|IDOC|SyncException|sync_error|KeyError|field.*not found", "SapOdoo", "SapOdoo Sync Mapping Error", new DateTime(2024, 1, 1) }
                });

            migrationBuilder.InsertData(
                table: "Solutions",
                columns: ["Id", "CreatedAt", "KnownIssueId", "References", "Steps", "Title", "Upvotes"],
                values: new object[,]
                {
                    { 1, new DateTime(2024, 1, 1), 1, "SAP Note 1234567", "1. Open SAP Management Console\n2. Navigate to SAP Gateway\n3. Stop and restart the gateway service\n4. Monitor connection logs for reconnection", "Restart SAP Gateway Service", 12 },
                    { 2, new DateTime(2024, 1, 1), 2, "Odoo Admin Guide — Timeouts", "1. Check Odoo server is running and reachable\n2. Increase xmlrpc timeout in SapOdoo config\n3. Check firewall rules between SAP and Odoo servers\n4. Verify Odoo URL and credentials in appsettings", "Check Odoo Server and Increase Timeout", 6 },
                    { 3, new DateTime(2024, 1, 1), 3, "SapOdoo Field Mapping Runbook", "1. Review the field mapping config in SapOdoo settings\n2. Compare SAP IDOC structure against Odoo model fields\n3. Add any missing field mappings and redeploy\n4. Re-run the failed sync batch from the SapOdoo admin panel", "Fix SapOdoo Field Mapping Config", 4 }
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
