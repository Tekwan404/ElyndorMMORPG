using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable IDE0161 // EF Core migration follows the repository's migration format.

namespace Elyndor.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(GameDbContext))]
    [Migration("20260929120000_ArenaMatchFormulaVersion")]
    public partial class ArenaMatchFormulaVersion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FormulaVersion",
                schema: "game",
                table: "arena_matches",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FormulaVersion",
                schema: "game",
                table: "arena_matches");
        }
    }
}
