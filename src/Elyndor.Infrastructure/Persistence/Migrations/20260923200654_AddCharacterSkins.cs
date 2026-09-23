using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddCharacterSkins : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ActiveSkinId",
            schema: "game",
            table: "characters",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "character_skin_ownerships",
            schema: "game",
            columns: table => new
            {
                CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                SkinId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                CrystalPrice = table.Column<long>(type: "bigint", nullable: false),
                PurchasedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_character_skin_ownerships", x => new { x.CharacterId, x.SkinId });
                table.CheckConstraint("ck_character_skin_ownerships_price_positive", "\"CrystalPrice\" > 0");
                table.ForeignKey(
                    name: "fk_character_skin_ownerships_characters_character_id",
                    column: x => x.CharacterId,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "uq_character_skin_ownerships_operation_id",
            schema: "game",
            table: "character_skin_ownerships",
            column: "OperationId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "character_skin_ownerships",
            schema: "game");

        migrationBuilder.DropColumn(
            name: "ActiveSkinId",
            schema: "game",
            table: "characters");
    }
}
