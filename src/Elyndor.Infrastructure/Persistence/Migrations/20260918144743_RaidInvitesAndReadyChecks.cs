using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class RaidInvitesAndReadyChecks : Migration
{
    private static readonly string[] RaidInviteTargetStatusColumns = ["TargetCharacterId", "Status"];
    private static readonly string[] RaidReadyCheckRaidStateColumns = ["RaidId", "State"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "raid_invites",
            schema: "game",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RaidId = table.Column<Guid>(type: "uuid", nullable: false),
                InviterCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                TargetCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                DecidedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_raid_invites", x => x.Id);
                table.ForeignKey(
                    name: "fk_raid_invites_inviter_character_id",
                    column: x => x.InviterCharacterId,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_raid_invites_raid_id",
                    column: x => x.RaidId,
                    principalSchema: "game",
                    principalTable: "raids",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_raid_invites_target_character_id",
                    column: x => x.TargetCharacterId,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "raid_ready_checks",
            schema: "game",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RaidId = table.Column<Guid>(type: "uuid", nullable: false),
                StartedByCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                State = table.Column<int>(type: "integer", nullable: false),
                StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_raid_ready_checks", x => x.Id);
                table.ForeignKey(
                    name: "fk_raid_ready_checks_raid_id",
                    column: x => x.RaidId,
                    principalSchema: "game",
                    principalTable: "raids",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_raid_ready_checks_started_by_character_id",
                    column: x => x.StartedByCharacterId,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_raid_invites_InviterCharacterId",
            schema: "game",
            table: "raid_invites",
            column: "InviterCharacterId");

        migrationBuilder.CreateIndex(
            name: "IX_raid_invites_RaidId",
            schema: "game",
            table: "raid_invites",
            column: "RaidId");

        migrationBuilder.CreateIndex(
            name: "ix_raid_invites_target_status",
            schema: "game",
            table: "raid_invites",
            columns: RaidInviteTargetStatusColumns);

        migrationBuilder.CreateIndex(
            name: "ix_raid_ready_checks_raid_state",
            schema: "game",
            table: "raid_ready_checks",
            columns: RaidReadyCheckRaidStateColumns);

        migrationBuilder.CreateIndex(
            name: "IX_raid_ready_checks_StartedByCharacterId",
            schema: "game",
            table: "raid_ready_checks",
            column: "StartedByCharacterId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "raid_invites",
            schema: "game");

        migrationBuilder.DropTable(
            name: "raid_ready_checks",
            schema: "game");
    }
}
