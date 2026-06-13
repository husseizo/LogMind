using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogMind.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalDependency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperationalDependencies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceSystem = table.Column<string>(type: "TEXT", nullable: false),
                    TargetSystem = table.Column<string>(type: "TEXT", nullable: false),
                    DependencyType = table.Column<string>(type: "TEXT", nullable: false),
                    Criticality = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    ImpactWeight = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalDependencies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_opdep_source",
                table: "OperationalDependencies",
                column: "SourceSystem");

            migrationBuilder.CreateIndex(
                name: "ix_opdep_active",
                table: "OperationalDependencies",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "OperationalDependencies");
        }
    }
}
