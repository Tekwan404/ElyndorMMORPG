using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20260908084500_FullQuestSystem")]
public partial class FullQuestSystem : Migration
{
    private static readonly string[] CharacterQuestStatusColumns =
        ["CharacterId", "Status"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "character_quest_states",
            schema: "game",
            columns: table => new
            {
                CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                QuestId = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: false),
                Status = table.Column<string>(
                    type: "character varying(24)",
                    maxLength: 24,
                    nullable: false),
                AcceptedAtUtc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false),
                ReadyAtUtc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: true),
                CompletedAtUtc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: true),
                ProgressJson = table.Column<string>(
                    type: "jsonb",
                    nullable: false),
                StateVersion = table.Column<long>(
                    type: "bigint",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "pk_character_quest_states",
                    x => new { x.CharacterId, x.QuestId });
                table.CheckConstraint(
                    "ck_character_quest_states_progress_json",
                    "jsonb_typeof(\"ProgressJson\") = 'object'");
                table.CheckConstraint(
                    "ck_character_quest_states_status",
                    "\"Status\" IN ('ACTIVE','READY_TO_CLAIM','COMPLETED')");
                table.CheckConstraint(
                    "ck_character_quest_states_version_positive",
                    "\"StateVersion\" > 0");
                table.ForeignKey(
                    name: "fk_character_quest_states_characters_character_id",
                    column: x => x.CharacterId,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "quest_reward_grants",
            schema: "game",
            columns: table => new
            {
                CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                QuestId = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: false),
                ClaimMutationId = table.Column<Guid>(
                    type: "uuid",
                    nullable: false),
                XpEarned = table.Column<int>(
                    type: "integer",
                    nullable: false),
                GoldEarned = table.Column<int>(
                    type: "integer",
                    nullable: false),
                RewardItemsJson = table.Column<string>(
                    type: "jsonb",
                    nullable: false),
                GrantedAtUtc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "pk_quest_reward_grants",
                    x => new { x.CharacterId, x.QuestId });
                table.CheckConstraint(
                    "ck_quest_reward_grants_gold_non_negative",
                    "\"GoldEarned\" >= 0");
                table.CheckConstraint(
                    "ck_quest_reward_grants_items_json",
                    "jsonb_typeof(\"RewardItemsJson\") = 'array'");
                table.CheckConstraint(
                    "ck_quest_reward_grants_xp_non_negative",
                    "\"XpEarned\" >= 0");
                table.ForeignKey(
                    name: "fk_quest_reward_grants_characters_character_id",
                    column: x => x.CharacterId,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_character_quest_states_character_status",
            schema: "game",
            table: "character_quest_states",
            columns: CharacterQuestStatusColumns);

        migrationBuilder.CreateIndex(
            name: "uq_quest_reward_grants_claim_mutation_id",
            schema: "game",
            table: "quest_reward_grants",
            column: "ClaimMutationId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "quest_reward_grants",
            schema: "game");

        migrationBuilder.DropTable(
            name: "character_quest_states",
            schema: "game");
    }
}
