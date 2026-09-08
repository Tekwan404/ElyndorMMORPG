using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class PhaseTwelveSharedLootResolution : Migration
{
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "combat_shared_loot_resolutions",
                schema: "game",
                columns: table => new
                {
                    CombatSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupLootJson = table.Column<string>(type: "jsonb", nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_combat_shared_loot_resolutions", x => x.CombatSessionId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "combat_shared_loot_resolutions",
                schema: "game");
        }
}
