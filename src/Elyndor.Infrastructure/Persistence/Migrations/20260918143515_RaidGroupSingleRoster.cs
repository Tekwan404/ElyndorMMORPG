using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class RaidGroupSingleRoster : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "raids",
            schema: "game",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CreationRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                LeaderCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                MaximumMembers = table.Column<int>(type: "integer", nullable: false),
                State = table.Column<int>(type: "integer", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_raids", x => x.Id);
                table.ForeignKey(
                    name: "fk_raids_leader_character",
                    column: x => x.LeaderCharacterId,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "raid_members",
            schema: "game",
            columns: table => new
            {
                RaidId = table.Column<Guid>(type: "uuid", nullable: false),
                CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                Role = table.Column<int>(type: "integer", nullable: false),
                State = table.Column<int>(type: "integer", nullable: false),
                JoinedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_raid_members", x => new { x.RaidId, x.CharacterId });
                table.ForeignKey(
                    name: "fk_raid_members_character_id",
                    column: x => x.CharacterId,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_raid_members_raids_raid_id",
                    column: x => x.RaidId,
                    principalSchema: "game",
                    principalTable: "raids",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "uq_raid_members_character_id",
            schema: "game",
            table: "raid_members",
            column: "CharacterId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_raids_leader_character_id",
            schema: "game",
            table: "raids",
            column: "LeaderCharacterId");

        migrationBuilder.CreateIndex(
            name: "uq_raids_creation_request_id",
            schema: "game",
            table: "raids",
            column: "CreationRequestId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "raid_members",
            schema: "game");

        migrationBuilder.DropTable(
            name: "raids",
            schema: "game");
    }
}
