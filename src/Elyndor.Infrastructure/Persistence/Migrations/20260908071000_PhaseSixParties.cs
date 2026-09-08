using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class PhaseSixParties : Migration
{
    private static readonly string[] PartyInviteIndexColumns = ["TargetCharacterId", "Status"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
            migrationBuilder.CreateTable(
                name: "parties",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreationRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaderCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parties", x => x.Id);
                    table.ForeignKey(
                        name: "fk_parties_leader_character",
                        column: x => x.LeaderCharacterId,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "party_invites",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviterCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_party_invites", x => x.Id);
                    table.ForeignKey(
                        name: "fk_party_invites_inviter_character_id",
                        column: x => x.InviterCharacterId,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_party_invites_party_id",
                        column: x => x.PartyId,
                        principalSchema: "game",
                        principalTable: "parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_party_invites_target_character_id",
                        column: x => x.TargetCharacterId,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "party_members",
                schema: "game",
                columns: table => new
                {
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_party_members", x => new { x.PartyId, x.CharacterId });
                    table.ForeignKey(
                        name: "fk_party_members_character_id",
                        column: x => x.CharacterId,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_party_members_parties_party_id",
                        column: x => x.PartyId,
                        principalSchema: "game",
                        principalTable: "parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_parties_leader_character_id",
                schema: "game",
                table: "parties",
                column: "LeaderCharacterId");

            migrationBuilder.CreateIndex(
                name: "uq_parties_creation_request_id",
                schema: "game",
                table: "parties",
                column: "CreationRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_party_invites_InviterCharacterId",
                schema: "game",
                table: "party_invites",
                column: "InviterCharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_party_invites_PartyId",
                schema: "game",
                table: "party_invites",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "ix_party_invites_target_status",
                schema: "game",
                table: "party_invites",
                columns: PartyInviteIndexColumns);

            migrationBuilder.CreateIndex(
                name: "uq_party_members_character_id",
                schema: "game",
                table: "party_members",
                column: "CharacterId",
                unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
            migrationBuilder.DropTable(
                name: "party_invites",
                schema: "game");

            migrationBuilder.DropTable(
                name: "party_members",
                schema: "game");

            migrationBuilder.DropTable(
                name: "parties",
                schema: "game");
    }
}
