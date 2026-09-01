using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class PhaseThreeTalentEngine : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "character_talent_states",
            schema: "game",
            columns: table => new
            {
                CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                TalentTreeId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ActiveLoadoutId = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                Loadout1RanksJson = table.Column<string>(type: "jsonb", nullable: false),
                Loadout2RanksJson = table.Column<string>(type: "jsonb", nullable: false),
                TalentVersion = table.Column<int>(type: "integer", nullable: false),
                StateVersion = table.Column<long>(type: "bigint", nullable: false),
                LastChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_character_talent_states", x => x.CharacterId);
                table.CheckConstraint("ck_character_talent_states_active_loadout", "\"ActiveLoadoutId\" IN ('LOADOUT_1', 'LOADOUT_2')");
                table.CheckConstraint("ck_character_talent_states_loadout_1_json", "jsonb_typeof(\"Loadout1RanksJson\") = 'object'");
                table.CheckConstraint("ck_character_talent_states_loadout_2_json", "jsonb_typeof(\"Loadout2RanksJson\") = 'object'");
                table.CheckConstraint("ck_character_talent_states_state_version", "\"StateVersion\" > 0");
                table.ForeignKey(
                    name: "fk_character_talent_states_characters_character_id",
                    column: x => x.CharacterId,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "character_talent_states",
            schema: "game");
    }
}
