using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class PhaseFiveProgressionLootEquipment : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "Experience",
            schema: "game",
            table: "characters",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.CreateTable(
            name: "character_items",
            schema: "game",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                ItemDefinitionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Quantity = table.Column<int>(type: "integer", nullable: false),
                AcquiredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_character_items", x => x.Id);
                table.CheckConstraint("ck_character_items_quantity_positive", "\"Quantity\" > 0");
                table.ForeignKey(
                    name: "fk_character_items_characters_character_id",
                    column: x => x.CharacterId,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "combat_reward_grants",
            schema: "game",
            columns: table => new
            {
                CombatSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                MonsterId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                XpEarned = table.Column<int>(type: "integer", nullable: false),
                GrantedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_combat_reward_grants", x => x.CombatSessionId);
                table.CheckConstraint("ck_combat_reward_grants_xp_non_negative", "\"XpEarned\" >= 0");
                table.ForeignKey(
                    name: "fk_combat_reward_grants_characters_character_id",
                    column: x => x.CharacterId,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "character_equipment",
            schema: "game",
            columns: table => new
            {
                CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                Slot = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                CharacterItemId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_character_equipment", x => new { x.CharacterId, x.Slot });
                table.ForeignKey(
                    name: "fk_character_equipment_character_items_item_id",
                    column: x => x.CharacterItemId,
                    principalSchema: "game",
                    principalTable: "character_items",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_character_equipment_characters_character_id",
                    column: x => x.CharacterId,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.AddCheckConstraint(
            name: "ck_characters_experience_non_negative",
            schema: "game",
            table: "characters",
            sql: "\"Experience\" >= 0");

        migrationBuilder.CreateIndex(
            name: "uq_character_equipment_item_id",
            schema: "game",
            table: "character_equipment",
            column: "CharacterItemId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_character_items_character_definition",
            schema: "game",
            table: "character_items",
            columns: ["CharacterId", "ItemDefinitionId"]);

        migrationBuilder.CreateIndex(
            name: "ix_combat_reward_grants_character_id",
            schema: "game",
            table: "combat_reward_grants",
            column: "CharacterId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "character_equipment",
            schema: "game");

        migrationBuilder.DropTable(
            name: "combat_reward_grants",
            schema: "game");

        migrationBuilder.DropTable(
            name: "character_items",
            schema: "game");

        migrationBuilder.DropCheckConstraint(
            name: "ck_characters_experience_non_negative",
            schema: "game",
            table: "characters");

        migrationBuilder.DropColumn(
            name: "Experience",
            schema: "game",
            table: "characters");
    }
}
