using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20260906074500_ItemInstancePrimaryStatRolls")]
public partial class ItemInstancePrimaryStatRolls : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "DefinitionVersion",
            schema: "game",
            table: "character_items",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<decimal>(
            name: "RolledAgility",
            schema: "game",
            table: "character_items",
            type: "numeric(18,4)",
            precision: 18,
            scale: 4,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "RolledIntellect",
            schema: "game",
            table: "character_items",
            type: "numeric(18,4)",
            precision: 18,
            scale: 4,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "RolledStamina",
            schema: "game",
            table: "character_items",
            type: "numeric(18,4)",
            precision: 18,
            scale: 4,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "RolledStrength",
            schema: "game",
            table: "character_items",
            type: "numeric(18,4)",
            precision: 18,
            scale: 4,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DefinitionVersion",
            schema: "game",
            table: "character_items");

        migrationBuilder.DropColumn(
            name: "RolledAgility",
            schema: "game",
            table: "character_items");

        migrationBuilder.DropColumn(
            name: "RolledIntellect",
            schema: "game",
            table: "character_items");

        migrationBuilder.DropColumn(
            name: "RolledStamina",
            schema: "game",
            table: "character_items");

        migrationBuilder.DropColumn(
            name: "RolledStrength",
            schema: "game",
            table: "character_items");
    }
}
