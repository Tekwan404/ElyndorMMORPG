using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20260920063000_ItemizationV2EnhancementSalvageRefunds")]
public partial class ItemizationV2EnhancementSalvageRefunds : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "EnhancementMaterialItemId",
            schema: "game",
            table: "item_salvage_operations",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "EnhancementMaterialQuantity",
            schema: "game",
            table: "item_salvage_operations",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "CatalystItemId",
            schema: "game",
            table: "item_salvage_operations",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "CatalystQuantity",
            schema: "game",
            table: "item_salvage_operations",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddCheckConstraint(
            name: "ck_item_salvage_operations_enhancement_material",
            schema: "game",
            table: "item_salvage_operations",
            sql: "\"EnhancementMaterialQuantity\" >= 0");

        migrationBuilder.AddCheckConstraint(
            name: "ck_item_salvage_operations_catalyst",
            schema: "game",
            table: "item_salvage_operations",
            sql: "\"CatalystQuantity\" >= 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_item_salvage_operations_enhancement_material",
            schema: "game",
            table: "item_salvage_operations");

        migrationBuilder.DropCheckConstraint(
            name: "ck_item_salvage_operations_catalyst",
            schema: "game",
            table: "item_salvage_operations");

        migrationBuilder.DropColumn(
            name: "EnhancementMaterialItemId",
            schema: "game",
            table: "item_salvage_operations");

        migrationBuilder.DropColumn(
            name: "EnhancementMaterialQuantity",
            schema: "game",
            table: "item_salvage_operations");

        migrationBuilder.DropColumn(
            name: "CatalystItemId",
            schema: "game",
            table: "item_salvage_operations");

        migrationBuilder.DropColumn(
            name: "CatalystQuantity",
            schema: "game",
            table: "item_salvage_operations");
    }
}
