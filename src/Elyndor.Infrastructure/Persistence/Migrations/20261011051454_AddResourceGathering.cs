using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;
/// <inheritdoc />
public partial class AddResourceGathering : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "resource_gathering_receipts",
            schema: "game",
            columns: table => new
            {
                CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                MutationId = table.Column<Guid>(type: "uuid", nullable: false),
                NodeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                ItemId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Quantity = table.Column<int>(type: "integer", nullable: false),
                SkillIncreased = table.Column<bool>(type: "boolean", nullable: false),
                GatheredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_resource_gathering_receipts", x => new { x.CharacterId, x.MutationId });
                table.CheckConstraint("ck_resource_gathering_quantity", "\"Quantity\" > 0");
                table.ForeignKey(
                    name: "FK_resource_gathering_receipts_characters_CharacterId",
                    column: x => x.CharacterId,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "resource_nodes",
            schema: "game",
            columns: table => new
            {
                node_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                available_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_resource_nodes", x => x.node_id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "resource_gathering_receipts",
            schema: "game");

        migrationBuilder.DropTable(
            name: "resource_nodes",
            schema: "game");
    }
}
