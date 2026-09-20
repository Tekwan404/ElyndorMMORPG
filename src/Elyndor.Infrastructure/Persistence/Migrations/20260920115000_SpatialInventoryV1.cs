using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20260920115000_SpatialInventoryV1")]
public partial class SpatialInventoryV1 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "character_spatial_artifacts",
            schema: "game",
            columns: table => new
            {
                CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                CharacterItemId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_character_spatial_artifacts", x => x.CharacterId);
                table.ForeignKey(
                    name: "fk_character_spatial_artifacts_characters_character_id",
                    column: x => x.CharacterId,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_character_spatial_artifacts_character_items_item_id",
                    column: x => x.CharacterItemId,
                    principalSchema: "game",
                    principalTable: "character_items",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "uq_character_spatial_artifacts_item_id",
            schema: "game",
            table: "character_spatial_artifacts",
            column: "CharacterItemId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "character_spatial_artifacts", schema: "game");
    }
}
