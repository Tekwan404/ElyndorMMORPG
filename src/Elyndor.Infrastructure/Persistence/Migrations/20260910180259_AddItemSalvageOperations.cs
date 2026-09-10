using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

    /// <inheritdoc />
public partial class AddItemSalvageOperations : Migration
    {
        private static readonly string[] CharacterItemColumns = ["CharacterId", "ItemInstanceId"];
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "item_salvage_operations",
                schema: "game",
                columns: table => new
                {
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemDefinitionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReforgeStoneItemId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReforgeStoneQuantity = table.Column<int>(type: "integer", nullable: false),
                    MaterialItemId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MaterialQuantity = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_item_salvage_operations", x => x.OperationId);
                    table.CheckConstraint("ck_item_salvage_operations_material", "\"MaterialQuantity\" >= 0");
                    table.CheckConstraint("ck_item_salvage_operations_stones", "\"ReforgeStoneQuantity\" > 0");
                });

            migrationBuilder.CreateIndex(
                name: "ix_item_salvage_operations_character_item",
                schema: "game",
                table: "item_salvage_operations",
                columns: CharacterItemColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "item_salvage_operations",
                schema: "game");
        }
    }
