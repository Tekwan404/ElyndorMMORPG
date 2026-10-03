using System;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20261003101500_WorldBossHealingContribution")]
public partial class WorldBossHealingContribution : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "Healing",
            schema: "game",
            table: "world_boss_contributions",
            type: "numeric(18,3)",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "Healing",
            schema: "game",
            table: "world_boss_party_contributions",
            type: "numeric(18,3)",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.DropIndex(
            name: "ix_world_boss_contributions_leaderboard",
            schema: "game",
            table: "world_boss_contributions");

        migrationBuilder.CreateIndex(
            name: "ix_world_boss_contributions_leaderboard",
            schema: "game",
            table: "world_boss_contributions",
            columns: new[] { "SpawnId", "Damage", "Healing" },
            descending: new[] { false, true, true });

        migrationBuilder.AddCheckConstraint(
            name: "ck_world_boss_contributions_healing_non_negative",
            schema: "game",
            table: "world_boss_contributions",
            sql: "\"Healing\" >= 0");

        migrationBuilder.AddCheckConstraint(
            name: "ck_world_boss_party_contributions_healing_non_negative",
            schema: "game",
            table: "world_boss_party_contributions",
            sql: "\"Healing\" >= 0");

        migrationBuilder.CreateTable(
            name: "world_boss_healing_mutations",
            schema: "game",
            columns: table => new
            {
                SpawnId = table.Column<Guid>(type: "uuid", nullable: false),
                MutationId = table.Column<Guid>(type: "uuid", nullable: false),
                CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                CombatSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                PartyId = table.Column<Guid>(type: "uuid", nullable: true),
                EffectiveHealing = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                CommittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "pk_world_boss_healing_mutations",
                    x => new { x.SpawnId, x.MutationId });
                table.CheckConstraint(
                    "ck_world_boss_healing_mutations_amount_positive",
                    "\"EffectiveHealing\" > 0");
                table.ForeignKey(
                    name: "fk_world_boss_healing_mutations_character",
                    column: x => x.CharacterId,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_world_boss_healing_mutations_party",
                    column: x => x.PartyId,
                    principalSchema: "game",
                    principalTable: "parties",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_world_boss_healing_mutations_spawn",
                    column: x => x.SpawnId,
                    principalSchema: "game",
                    principalTable: "world_boss_spawns",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_world_boss_healing_mutations_character",
            schema: "game",
            table: "world_boss_healing_mutations",
            column: "CharacterId");

        migrationBuilder.CreateIndex(
            name: "ix_world_boss_healing_mutations_party",
            schema: "game",
            table: "world_boss_healing_mutations",
            column: "PartyId");

        migrationBuilder.CreateIndex(
            name: "ix_world_boss_healing_mutations_combat_session",
            schema: "game",
            table: "world_boss_healing_mutations",
            column: "CombatSessionId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "world_boss_healing_mutations",
            schema: "game");

        migrationBuilder.DropCheckConstraint(
            name: "ck_world_boss_contributions_healing_non_negative",
            schema: "game",
            table: "world_boss_contributions");

        migrationBuilder.DropCheckConstraint(
            name: "ck_world_boss_party_contributions_healing_non_negative",
            schema: "game",
            table: "world_boss_party_contributions");

        migrationBuilder.DropIndex(
            name: "ix_world_boss_contributions_leaderboard",
            schema: "game",
            table: "world_boss_contributions");

        migrationBuilder.CreateIndex(
            name: "ix_world_boss_contributions_leaderboard",
            schema: "game",
            table: "world_boss_contributions",
            columns: new[] { "SpawnId", "Damage" },
            descending: new[] { false, true });

        migrationBuilder.DropColumn(
            name: "Healing",
            schema: "game",
            table: "world_boss_contributions");

        migrationBuilder.DropColumn(
            name: "Healing",
            schema: "game",
            table: "world_boss_party_contributions");
    }
}
