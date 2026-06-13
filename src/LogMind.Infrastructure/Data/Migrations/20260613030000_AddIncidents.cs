using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMind.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Incidents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    IncidentFingerprint = table.Column<string>(type: "TEXT", nullable: false),
                    RootCauseSummary = table.Column<string>(type: "TEXT", nullable: false),
                    RootLogMessage = table.Column<string>(type: "TEXT", nullable: false),
                    SourceSystem = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "High"),
                    Status = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Open"),
                    EventCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    FirstSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incidents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IncidentEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IncidentId = table.Column<int>(type: "INTEGER", nullable: false),
                    LogEntryId = table.Column<int>(type: "INTEGER", nullable: false),
                    CorrelationScore = table.Column<float>(type: "REAL", nullable: false, defaultValue: 0f),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    CorrelationBasis = table.Column<string>(type: "TEXT", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    MinutesFromRoot = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentEvents_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IncidentEvents_LogEntries_LogEntryId",
                        column: x => x.LogEntryId,
                        principalTable: "LogEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_incident_source_lastseen",
                table: "Incidents",
                columns: new[] { "SourceSystem", "LastSeenAt" });

            migrationBuilder.CreateIndex(
                name: "ix_incident_status",
                table: "Incidents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_incident_fingerprint",
                table: "Incidents",
                column: "IncidentFingerprint");

            migrationBuilder.CreateIndex(
                name: "ix_incident_event_logentry",
                table: "IncidentEvents",
                column: "LogEntryId");

            migrationBuilder.CreateIndex(
                name: "ix_incident_event_sequence",
                table: "IncidentEvents",
                columns: new[] { "IncidentId", "Sequence" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "IncidentEvents");
            migrationBuilder.DropTable(name: "Incidents");
        }
    }
}
