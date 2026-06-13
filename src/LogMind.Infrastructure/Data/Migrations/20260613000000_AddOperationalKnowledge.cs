using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMind.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalKnowledge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperationalKnowledge",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    System = table.Column<string>(type: "TEXT", nullable: false),
                    Tags = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    ApplicableSources = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
                    EmbeddingVector = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalKnowledge", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_opknowledge_active",
                table: "OperationalKnowledge",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "ix_opknowledge_category",
                table: "OperationalKnowledge",
                column: "Category");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "OperationalKnowledge");
        }
    }
}
