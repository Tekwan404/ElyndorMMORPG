using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;
/// <inheritdoc />
public partial class StorePersonalization : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ActiveProfileFrameId",
            schema: "game",
            table: "characters",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ActiveTitleId",
            schema: "game",
            table: "characters",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "character_cosmetic_ownerships",
            schema: "game",
            columns: table => new
            {
                CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                CosmeticId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                CrystalPrice = table.Column<long>(type: "bigint", nullable: false),
                PurchasedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_character_cosmetic_ownerships", x => new { x.CharacterId, x.CosmeticId });
                table.CheckConstraint("ck_character_cosmetic_ownerships_price_positive", "\"CrystalPrice\" > 0");
                table.ForeignKey(
                    name: "fk_character_cosmetic_ownerships_character",
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
            name: "character_cosmetic_ownerships",
            schema: "game");

        migrationBuilder.DropColumn(
            name: "ActiveProfileFrameId",
            schema: "game",
            table: "characters");

        migrationBuilder.DropColumn(
            name: "ActiveTitleId",
            schema: "game",
            table: "characters");
    }
}
