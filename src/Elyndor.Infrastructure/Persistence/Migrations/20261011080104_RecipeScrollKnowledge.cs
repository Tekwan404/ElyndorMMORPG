using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;
/// <inheritdoc />
public partial class RecipeScrollKnowledge : Migration
{
    private static readonly string[] ProfessionKeyColumns = ["character_id", "profession_id"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "character_recipe_knowledge",
            schema: "game",
            columns: table => new
            {
                CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                ProfessionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                RecipeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                LearnedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_character_recipe_knowledge", x => new { x.CharacterId, x.ProfessionId, x.RecipeId });
                table.ForeignKey(
                    name: "FK_character_recipe_knowledge_character_professions_CharacterI~",
                    columns: x => new { x.CharacterId, x.ProfessionId },
                    principalSchema: "game",
                    principalTable: "character_professions",
                    principalColumns: ProfessionKeyColumns,
                    onDelete: ReferentialAction.Cascade);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "character_recipe_knowledge",
            schema: "game");
    }
}

