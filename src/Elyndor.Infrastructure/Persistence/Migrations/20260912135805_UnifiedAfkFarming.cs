using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

    /// <inheritdoc />
public partial class UnifiedAfkFarming : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Mode",
                schema: "game",
                table: "afk_farm_sessions");

            migrationBuilder.AddColumn<string>(
                name: "TargetMonsterId",
                schema: "game",
                table: "afk_farm_sessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetMonsterId",
                schema: "game",
                table: "afk_farm_sessions");

            migrationBuilder.AddColumn<string>(
                name: "Mode",
                schema: "game",
                table: "afk_farm_sessions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");
        }
}
