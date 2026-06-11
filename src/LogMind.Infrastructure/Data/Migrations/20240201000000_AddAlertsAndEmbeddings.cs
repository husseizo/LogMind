using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMind.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertsAndEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Alerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RuleName         = table.Column<string>(type: "TEXT", nullable: false),
                    Source           = table.Column<string>(type: "TEXT", nullable: false),
                    Pattern          = table.Column<string>(type: "TEXT", nullable: false),
                    OccurrenceCount  = table.Column<int>(type: "INTEGER", nullable: false),
                    ThresholdCount   = table.Column<int>(type: "INTEGER", nullable: false),
                    WindowMinutes    = table.Column<int>(type: "INTEGER", nullable: false),
                    SampleMessage    = table.Column<string>(type: "TEXT", nullable: false),
                    IsAcknowledged   = table.Column<bool>(type: "INTEGER", nullable: false),
                    TriggeredAt      = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AcknowledgedAt   = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_Alerts", x => x.Id));

            migrationBuilder.CreateIndex(name: "IX_Alerts_IsAcknowledged", table: "Alerts", column: "IsAcknowledged");
            migrationBuilder.CreateIndex(name: "IX_Alerts_Source",        table: "Alerts", column: "Source");
            migrationBuilder.CreateIndex(name: "IX_Alerts_TriggeredAt",   table: "Alerts", column: "TriggeredAt");

            migrationBuilder.CreateTable(
                name: "KnownIssueEmbeddings",
                columns: table => new
                {
                    Id           = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KnownIssueId = table.Column<int>(type: "INTEGER", nullable: false),
                    VectorJson   = table.Column<string>(type: "TEXT", nullable: false),
                    GeneratedAt  = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnownIssueEmbeddings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnownIssueEmbeddings_KnownIssues_KnownIssueId",
                        column: x => x.KnownIssueId,
                        principalTable: "KnownIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnownIssueEmbeddings_KnownIssueId",
                table: "KnownIssueEmbeddings",
                column: "KnownIssueId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Alerts");
            migrationBuilder.DropTable(name: "KnownIssueEmbeddings");
        }
    }
}
