using System;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20261002073500_WorldBossCombatBridge")]
public partial class WorldBossCombatBridge : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "world_boss_combat_sessions",
            schema: "game",
            columns: table => new
            {
                CombatSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                SpawnId = table.Column<Guid>(type: "uuid", nullable: false),
                BossActorId = table.Column<Guid>(type: "uuid", nullable: false),
                PartyId = table.Column<Guid>(type: "uuid", nullable: true),
                BoundAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_world_boss_combat_sessions", x => x.CombatSessionId);
                table.ForeignKey(
                    name: "fk_world_boss_combat_sessions_party",
                    column: x => x.PartyId,
                    principalSchema: "game",
                    principalTable: "parties",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_world_boss_combat_sessions_spawn",
                    column: x => x.SpawnId,
                    principalSchema: "game",
                    principalTable: "world_boss_spawns",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_world_boss_combat_sessions_party",
            schema: "game",
            table: "world_boss_combat_sessions",
            column: "PartyId");

        migrationBuilder.CreateIndex(
            name: "ix_world_boss_combat_sessions_spawn",
            schema: "game",
            table: "world_boss_combat_sessions",
            column: "SpawnId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "world_boss_combat_sessions",
            schema: "game");
    }
}
