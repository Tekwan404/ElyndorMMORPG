using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;
    /// <inheritdoc />
public partial class PhaseNinePersistedCombatLootRolls : Migration
{
    private static readonly string[] SessionItemColumns = ["CombatSessionId", "ItemDefinitionId"];
    private static readonly string[] StateEndsColumns = ["State", "EndsAtUtc"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "combat_loot_rolls",
                schema: "game",
                columns: table => new
                {
                    LootRollId = table.Column<Guid>(type: "uuid", nullable: false),
                    DungeonRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    CombatSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemDefinitionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Rarity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    ItemInstanceSeed = table.Column<Guid>(type: "uuid", nullable: false),
                    EligibleCharacterIdsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ChoicesJson = table.Column<string>(type: "jsonb", nullable: false),
                    RollsJson = table.Column<string>(type: "jsonb", nullable: false),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    WinnerCharacterId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_combat_loot_rolls", x => x.LootRollId);
                    table.CheckConstraint("ck_combat_loot_rolls_quantity_positive", "\"Quantity\" > 0");
                });

            migrationBuilder.CreateIndex(
                name: "ix_combat_loot_rolls_state_ends_at_utc",
                schema: "game",
                table: "combat_loot_rolls",
                columns: StateEndsColumns);

            migrationBuilder.CreateIndex(
                name: "uq_combat_loot_rolls_session_item",
                schema: "game",
                table: "combat_loot_rolls",
                columns: SessionItemColumns,
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "combat_loot_rolls",
                schema: "game");
        }
    }
