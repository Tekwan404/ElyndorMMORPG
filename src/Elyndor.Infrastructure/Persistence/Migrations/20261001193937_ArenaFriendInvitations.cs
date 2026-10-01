using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF-generated migration uses fixed index column arrays.

namespace Elyndor.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class ArenaFriendInvitations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "arena_invitations",
            schema: "game",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InviterCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                TargetCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                MatchId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_arena_invitations", x => x.Id);
                table.CheckConstraint("ck_arena_invitation_distinct", "\"InviterCharacterId\" <> \"TargetCharacterId\"");
                table.CheckConstraint("ck_arena_invitation_expiry", "\"ExpiresAtUtc\" > \"CreatedAtUtc\"");
                table.ForeignKey(
                    name: "FK_arena_invitations_arena_matches_MatchId",
                    column: x => x.MatchId,
                    principalSchema: "game",
                    principalTable: "arena_matches",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_arena_invitations_characters_InviterCharacterId",
                    column: x => x.InviterCharacterId,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_arena_invitations_characters_TargetCharacterId",
                    column: x => x.TargetCharacterId,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_arena_invitations_InviterCharacterId_CreatedAtUtc",
            schema: "game",
            table: "arena_invitations",
            columns: new[] { "InviterCharacterId", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_arena_invitations_MatchId",
            schema: "game",
            table: "arena_invitations",
            column: "MatchId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_arena_invitations_TargetCharacterId_Status_ExpiresAtUtc",
            schema: "game",
            table: "arena_invitations",
            columns: new[] { "TargetCharacterId", "Status", "ExpiresAtUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "arena_invitations",
            schema: "game");
    }
}
