using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LogMind.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExplanationCacheAndFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NeedsReview",
                table: "Solutions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastOccurredAt",
                table: "KnownIssues",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AiExplanationCache",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LogEntryId = table.Column<int>(type: "INTEGER", nullable: true),
                    MessageHash = table.Column<string>(type: "TEXT", nullable: false),
                    NormalizedMessage = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    Level = table.Column<string>(type: "TEXT", nullable: false),
                    Model = table.Column<string>(type: "TEXT", nullable: false),
                    PromptVersion = table.Column<string>(type: "TEXT", nullable: false),
                    ExplanationJson = table.Column<string>(type: "TEXT", nullable: false),
                    EmbeddingVector = table.Column<string>(type: "TEXT", nullable: true),
                    HitCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsInvalidated = table.Column<bool>(type: "INTEGER", nullable: false),
                    RelatedIssueId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiExplanationCache", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiExplanationCache_KnownIssues_RelatedIssueId",
                        column: x => x.RelatedIssueId,
                        principalTable: "KnownIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AiExplanationCache_LogEntries_LogEntryId",
                        column: x => x.LogEntryId,
                        principalTable: "LogEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SolutionFeedback",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SolutionId = table.Column<int>(type: "INTEGER", nullable: false),
                    LogEntryId = table.Column<int>(type: "INTEGER", nullable: true),
                    Worked = table.Column<bool>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolutionFeedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolutionFeedback_LogEntries_LogEntryId",
                        column: x => x.LogEntryId,
                        principalTable: "LogEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SolutionFeedback_Solutions_SolutionId",
                        column: x => x.SolutionId,
                        principalTable: "Solutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_logs_source_level",
                table: "LogEntries",
                columns: new[] { "Source", "Level" });

            migrationBuilder.CreateIndex(
                name: "ix_logs_timestamp_id",
                table: "LogEntries",
                columns: new[] { "Timestamp", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AiExplanationCache_LogEntryId",
                table: "AiExplanationCache",
                column: "LogEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_AiExplanationCache_RelatedIssueId",
                table: "AiExplanationCache",
                column: "RelatedIssueId");

            migrationBuilder.CreateIndex(
                name: "ix_cache_hash",
                table: "AiExplanationCache",
                column: "MessageHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cache_invalidated_lastused",
                table: "AiExplanationCache",
                columns: new[] { "IsInvalidated", "LastUsedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_cache_source_createdat",
                table: "AiExplanationCache",
                columns: new[] { "Source", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_feedback_logentryid",
                table: "SolutionFeedback",
                column: "LogEntryId");

            migrationBuilder.CreateIndex(
                name: "ix_feedback_solutionid",
                table: "SolutionFeedback",
                column: "SolutionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiExplanationCache");

            migrationBuilder.DropTable(
                name: "SolutionFeedback");

            migrationBuilder.DropIndex(
                name: "ix_logs_source_level",
                table: "LogEntries");

            migrationBuilder.DropIndex(
                name: "ix_logs_timestamp_id",
                table: "LogEntries");

            migrationBuilder.DropColumn(
                name: "NeedsReview",
                table: "Solutions");

            migrationBuilder.DropColumn(
                name: "LastOccurredAt",
                table: "KnownIssues");
        }
    }
}
