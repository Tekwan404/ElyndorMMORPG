using System;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Elyndor.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20261002070000_WorldBossV1Foundation")]
public partial class WorldBossV1Foundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "world_boss_spawns",
            schema: "game",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                BossDefinitionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                MaxHealth = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                CurrentHealth = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                CurrentPhase = table.Column<int>(type: "integer", nullable: false),
                SpawnedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                DefeatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                SettledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                ContentVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                BalanceVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_world_boss_spawns", x => x.Id);
                table.CheckConstraint("ck_world_boss_spawns_expiry_after_spawn", "\"ExpiresAtUtc\" > \"SpawnedAtUtc\"");
                table.CheckConstraint("ck_world_boss_spawns_health_range", "\"CurrentHealth\" >= 0 AND \"CurrentHealth\" <= \"MaxHealth\"");
                table.CheckConstraint("ck_world_boss_spawns_max_health_positive", "\"MaxHealth\" > 0");
                table.CheckConstraint("ck_world_boss_spawns_phase_positive", "\"CurrentPhase\" > 0");
            });

        migrationBuilder.CreateTable(
            name: "world_boss_contributions",
            schema: "game",
            columns: table => new
            {
                SpawnId = table.Column<Guid>(type: "uuid", nullable: false),
                CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                Damage = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                FirstActivityAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LastActivityAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_world_boss_contributions", x => new { x.SpawnId, x.CharacterId });
                table.CheckConstraint("ck_world_boss_contributions_activity_order", "\"LastActivityAtUtc\" >= \"FirstActivityAtUtc\"");
                table.CheckConstraint("ck_world_boss_contributions_damage_non_negative", "\"Damage\" >= 0");
                table.ForeignKey(
                    name: "fk_world_boss_contributions_character",
                    column: x => x.CharacterId,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_world_boss_contributions_spawn",
                    column: x => x.SpawnId,
                    principalSchema: "game",
                    principalTable: "world_boss_spawns",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "world_boss_party_contributions",
            schema: "game",
            columns: table => new
            {
                SpawnId = table.Column<Guid>(type: "uuid", nullable: false),
                PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                Damage = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_world_boss_party_contributions", x => new { x.SpawnId, x.PartyId });
                table.CheckConstraint("ck_world_boss_party_contributions_damage_non_negative", "\"Damage\" >= 0");
                table.ForeignKey(
                    name: "fk_world_boss_party_contributions_party",
                    column: x => x.PartyId,
                    principalSchema: "game",
                    principalTable: "parties",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_world_boss_party_contributions_spawn",
                    column: x => x.SpawnId,
                    principalSchema: "game",
                    principalTable: "world_boss_spawns",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "world_boss_damage_mutations",
            schema: "game",
            columns: table => new
            {
                SpawnId = table.Column<Guid>(type: "uuid", nullable: false),
                MutationId = table.Column<Guid>(type: "uuid", nullable: false),
                CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                CombatSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                PartyId = table.Column<Guid>(type: "uuid", nullable: true),
                RequestedDamage = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                AppliedDamage = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                CommittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_world_boss_damage_mutations", x => new { x.SpawnId, x.MutationId });
                table.CheckConstraint("ck_world_boss_damage_mutations_applied_range", "\"AppliedDamage\" >= 0 AND \"AppliedDamage\" <= \"RequestedDamage\"");
                table.CheckConstraint("ck_world_boss_damage_mutations_requested_non_negative", "\"RequestedDamage\" >= 0");
                table.ForeignKey(
                    name: "fk_world_boss_damage_mutations_character",
                    column: x => x.CharacterId,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_world_boss_damage_mutations_party",
                    column: x => x.PartyId,
                    principalSchema: "game",
                    principalTable: "parties",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_world_boss_damage_mutations_spawn",
                    column: x => x.SpawnId,
                    principalSchema: "game",
                    principalTable: "world_boss_spawns",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "world_boss_reward_settlements",
            schema: "game",
            columns: table => new
            {
                SpawnId = table.Column<Guid>(type: "uuid", nullable: false),
                CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                ContributionScore = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                RewardTier = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                Gold = table.Column<int>(type: "integer", nullable: false),
                Experience = table.Column<int>(type: "integer", nullable: false),
                Tokens = table.Column<int>(type: "integer", nullable: false),
                LootRollSeed = table.Column<Guid>(type: "uuid", nullable: false),
                LootResultJson = table.Column<string>(type: "jsonb", nullable: false),
                SettledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_world_boss_reward_settlements", x => new { x.SpawnId, x.CharacterId });
                table.CheckConstraint("ck_world_boss_reward_settlements_rewards_non_negative", "\"Gold\" >= 0 AND \"Experience\" >= 0 AND \"Tokens\" >= 0");
                table.CheckConstraint("ck_world_boss_reward_settlements_score_non_negative", "\"ContributionScore\" >= 0");
                table.ForeignKey(
                    name: "fk_world_boss_reward_settlements_character",
                    column: x => x.CharacterId,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_world_boss_reward_settlements_spawn",
                    column: x => x.SpawnId,
                    principalSchema: "game",
                    principalTable: "world_boss_spawns",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_world_boss_spawns_definition_spawned",
            schema: "game",
            table: "world_boss_spawns",
            columns: new[] { "BossDefinitionId", "SpawnedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "ix_world_boss_spawns_status_expires",
            schema: "game",
            table: "world_boss_spawns",
            columns: new[] { "Status", "ExpiresAtUtc" });

        migrationBuilder.CreateIndex(
            name: "ix_world_boss_contributions_character_activity",
            schema: "game",
            table: "world_boss_contributions",
            columns: new[] { "CharacterId", "LastActivityAtUtc" });

        migrationBuilder.CreateIndex(
            name: "ix_world_boss_contributions_leaderboard",
            schema: "game",
            table: "world_boss_contributions",
            columns: new[] { "SpawnId", "Damage" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "ix_world_boss_party_contributions_leaderboard",
            schema: "game",
            table: "world_boss_party_contributions",
            columns: new[] { "SpawnId", "Damage" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "IX_world_boss_party_contributions_PartyId",
            schema: "game",
            table: "world_boss_party_contributions",
            column: "PartyId");

        migrationBuilder.CreateIndex(
            name: "ix_world_boss_damage_mutations_character_committed",
            schema: "game",
            table: "world_boss_damage_mutations",
            columns: new[] { "CharacterId", "CommittedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "ix_world_boss_damage_mutations_combat_session",
            schema: "game",
            table: "world_boss_damage_mutations",
            column: "CombatSessionId");

        migrationBuilder.CreateIndex(
            name: "IX_world_boss_damage_mutations_PartyId",
            schema: "game",
            table: "world_boss_damage_mutations",
            column: "PartyId");

        migrationBuilder.CreateIndex(
            name: "ix_world_boss_reward_settlements_character_settled",
            schema: "game",
            table: "world_boss_reward_settlements",
            columns: new[] { "CharacterId", "SettledAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "world_boss_damage_mutations", schema: "game");
        migrationBuilder.DropTable(name: "world_boss_party_contributions", schema: "game");
        migrationBuilder.DropTable(name: "world_boss_reward_settlements", schema: "game");
        migrationBuilder.DropTable(name: "world_boss_contributions", schema: "game");
        migrationBuilder.DropTable(name: "world_boss_spawns", schema: "game");
    }
}
