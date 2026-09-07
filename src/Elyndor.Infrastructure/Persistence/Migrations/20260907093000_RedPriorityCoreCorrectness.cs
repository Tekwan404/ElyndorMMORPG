using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20260907093000_RedPriorityCoreCorrectness")]
public partial class RedPriorityCoreCorrectness : Migration
{
    private static readonly string[] CharacterTravelRequestColumns =
        ["CharacterId", "RequestId"];
    private static readonly string[] PendingLootCharacterCreatedColumns =
        ["CharacterId", "CreatedAtUtc"];
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "active_combat_sessions",
            schema: "game",
            columns: table => new
            {
                SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                StartedAtUtc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false),
                ContentVersion = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false),
                BalanceVersion = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "pk_active_combat_sessions",
                    x => x.SessionId);
                table.ForeignKey(
                    name: "fk_active_combat_sessions_characters_character_id",
                    column: x => x.CharacterId,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "character_travel_states",
            schema: "game",
            columns: table => new
            {
                CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                FromLocationId = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: false),
                TargetLocationId = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: false),
                StartedAtUtc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false),
                EndsAtUtc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "pk_character_travel_states",
                    x => x.CharacterId);
                table.CheckConstraint(
                    "ck_character_travel_states_duration",
                    "\"EndsAtUtc\" > \"StartedAtUtc\"");
                table.ForeignKey(
                    name: "fk_character_travel_states_characters_character_id",
                    column: x => x.CharacterId,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "pending_loot_items",
            schema: "game",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                RewardResolutionId = table.Column<Guid>(type: "uuid", nullable: false),
                ItemDefinitionId = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: false),
                Quantity = table.Column<int>(type: "integer", nullable: false),
                DefinitionVersion = table.Column<int>(type: "integer", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false),
                RolledStrength = table.Column<decimal>(
                    type: "numeric(18,4)",
                    precision: 18,
                    scale: 4,
                    nullable: true),
                RolledAgility = table.Column<decimal>(
                    type: "numeric(18,4)",
                    precision: 18,
                    scale: 4,
                    nullable: true),
                RolledIntellect = table.Column<decimal>(
                    type: "numeric(18,4)",
                    precision: 18,
                    scale: 4,
                    nullable: true),
                RolledStamina = table.Column<decimal>(
                    type: "numeric(18,4)",
                    precision: 18,
                    scale: 4,
                    nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_pending_loot_items", x => x.Id);
                table.CheckConstraint(
                    "ck_pending_loot_items_quantity_positive",
                    "\"Quantity\" > 0");
                table.ForeignKey(
                    name: "fk_pending_loot_items_characters_character_id",
                    column: x => x.CharacterId,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "combat_consumable_uses",
            schema: "game",
            columns: table => new
            {
                SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                CommandId = table.Column<string>(
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: false),
                CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                ItemDefinitionId = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: false),
                DefinitionVersion = table.Column<int>(
                    type: "integer",
                    nullable: false),
                MaxStack = table.Column<int>(
                    type: "integer",
                    nullable: false),
                UsedAtUtc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "pk_combat_consumable_uses",
                    x => new { x.SessionId, x.CommandId });
                table.CheckConstraint(
                    "ck_combat_consumable_uses_definition_version",
                    "\"DefinitionVersion\" > 0");
                table.CheckConstraint(
                    "ck_combat_consumable_uses_max_stack",
                    "\"MaxStack\" >= 2");
                table.ForeignKey(
                    name: "fk_combat_consumable_uses_active_combat_session_id",
                    column: x => x.SessionId,
                    principalSchema: "game",
                    principalTable: "active_combat_sessions",
                    principalColumn: "SessionId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "uq_active_combat_sessions_character_id",
            schema: "game",
            table: "active_combat_sessions",
            column: "CharacterId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_active_combat_sessions_started_at_utc",
            schema: "game",
            table: "active_combat_sessions",
            column: "StartedAtUtc");

        migrationBuilder.CreateIndex(
            name: "ix_character_travel_states_ends_at_utc",
            schema: "game",
            table: "character_travel_states",
            column: "EndsAtUtc");

        migrationBuilder.CreateIndex(
            name: "uq_character_travel_states_character_request",
            schema: "game",
            table: "character_travel_states",
            columns: CharacterTravelRequestColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_pending_loot_items_character_created_at",
            schema: "game",
            table: "pending_loot_items",
            columns: PendingLootCharacterCreatedColumns);

        migrationBuilder.CreateIndex(
            name: "ix_pending_loot_items_reward_resolution_id",
            schema: "game",
            table: "pending_loot_items",
            column: "RewardResolutionId");

        migrationBuilder.CreateIndex(
            name: "ix_combat_consumable_uses_character_id",
            schema: "game",
            table: "combat_consumable_uses",
            column: "CharacterId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "combat_consumable_uses",
            schema: "game");

        migrationBuilder.DropTable(
            name: "character_travel_states",
            schema: "game");

        migrationBuilder.DropTable(
            name: "pending_loot_items",
            schema: "game");

        migrationBuilder.DropTable(
            name: "active_combat_sessions",
            schema: "game");
    }
}
