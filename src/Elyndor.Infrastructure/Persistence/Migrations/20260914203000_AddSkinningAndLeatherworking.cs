using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20260914203000_AddSkinningAndLeatherworking")]
public partial class AddSkinningAndLeatherworking : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "character_professions",
            schema: "game",
            columns: table => new
            {
                character_id = table.Column<Guid>(type: "uuid", nullable: false),
                profession_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                skill = table.Column<int>(type: "integer", nullable: false),
                learned_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_character_professions", x => new { x.character_id, x.profession_id });
                table.ForeignKey(
                    name: "FK_character_professions_characters_character_id",
                    column: x => x.character_id,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "character_skinnable_corpses",
            schema: "game",
            columns: table => new
            {
                character_id = table.Column<Guid>(type: "uuid", nullable: false),
                combat_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                enemy_actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                monster_definition_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                skinned_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                skinning_mutation_id = table.Column<Guid>(type: "uuid", nullable: true),
                yield_item_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                yield_quantity = table.Column<int>(type: "integer", nullable: true),
                skill_increased = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_character_skinnable_corpses", x => new { x.character_id, x.combat_session_id, x.enemy_actor_id });
                table.ForeignKey(
                    name: "FK_character_skinnable_corpses_characters_character_id",
                    column: x => x.character_id,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_character_professions_character_id",
            schema: "game",
            table: "character_professions",
            column: "character_id");

        migrationBuilder.CreateIndex(
            name: "IX_character_skinnable_corpses_character_id_expires_at_utc",
            schema: "game",
            table: "character_skinnable_corpses",
            columns: new[] { "character_id", "expires_at_utc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "character_skinnable_corpses", schema: "game");
        migrationBuilder.DropTable(name: "character_professions", schema: "game");
    }
}
