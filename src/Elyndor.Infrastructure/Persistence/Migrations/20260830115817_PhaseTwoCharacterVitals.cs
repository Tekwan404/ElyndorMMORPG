using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class PhaseTwoCharacterVitals : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "character_vitals",
            schema: "game",
            columns: table => new
            {
                CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                CurrentHp = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                CurrentResource = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                CheckpointedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ContextStartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_character_vitals", x => x.CharacterId);
                table.CheckConstraint("ck_character_vitals_hp_non_negative", "\"CurrentHp\" >= 0");
                table.CheckConstraint("ck_character_vitals_resource_non_negative", "\"CurrentResource\" >= 0");
                table.ForeignKey(
                    name: "fk_character_vitals_characters_character_id",
                    column: x => x.CharacterId,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.Sql(
            """
            INSERT INTO game.character_vitals
                ("CharacterId", "CurrentHp", "CurrentResource", "CheckpointedAtUtc", "ContextStartedAtUtc")
            SELECT
                "Id",
                50 + 10 * (
                    CASE "ClassId"
                        WHEN 'WARRIOR' THEN 10 + ("Level" - 1) * 2
                        WHEN 'ARCHER' THEN 7 + ("Level" - 1) * 2
                        WHEN 'MAGE' THEN 6 + ("Level" - 1) * 2
                        ELSE 0
                    END),
                CASE "ClassId" WHEN 'WARRIOR' THEN 0 ELSE 100 END,
                "CreatedAtUtc",
                "CreatedAtUtc"
            FROM game.characters;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "character_vitals",
            schema: "game");
    }
}
