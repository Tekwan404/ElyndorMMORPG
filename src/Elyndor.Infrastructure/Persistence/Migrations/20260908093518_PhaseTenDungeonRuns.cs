using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

public partial class PhaseTenDungeonRuns : Migration
{
    private static readonly string[] RunEncounterColumns = ["RunId", "EncounterIndex"];
    private static readonly string[] RunStateColumns = ["PartyId", "State"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dungeon_runs",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DungeonId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CurrentEncounterIndex = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dungeon_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "dungeon_encounters",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    EncounterIndex = table.Column<int>(type: "integer", nullable: false),
                    MonsterId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CombatSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    WipeCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dungeon_encounters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_dungeon_encounters_dungeon_runs_RunId",
                        column: x => x.RunId,
                        principalSchema: "game",
                        principalTable: "dungeon_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dungeon_run_members",
                schema: "game",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dungeon_run_members", x => new { x.RunId, x.CharacterId });
                    table.ForeignKey(
                        name: "FK_dungeon_run_members_dungeon_runs_RunId",
                        column: x => x.RunId,
                        principalSchema: "game",
                        principalTable: "dungeon_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dungeon_encounter_members",
                schema: "game",
                columns: table => new
                {
                    EncounterId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dungeon_encounter_members", x => new { x.EncounterId, x.CharacterId });
                    table.ForeignKey(
                        name: "FK_dungeon_encounter_members_dungeon_encounters_EncounterId",
                        column: x => x.EncounterId,
                        principalSchema: "game",
                        principalTable: "dungeon_encounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dungeon_encounter_members_character_id",
                schema: "game",
                table: "dungeon_encounter_members",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "ix_dungeon_encounters_combat_session_id",
                schema: "game",
                table: "dungeon_encounters",
                column: "CombatSessionId");

            migrationBuilder.CreateIndex(
                name: "uq_dungeon_encounters_run_index",
                schema: "game",
                table: "dungeon_encounters",
                columns: RunEncounterColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dungeon_run_members_character_id",
                schema: "game",
                table: "dungeon_run_members",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "ix_dungeon_runs_party_state",
                schema: "game",
                table: "dungeon_runs",
                columns: RunStateColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dungeon_encounter_members",
                schema: "game");

            migrationBuilder.DropTable(
                name: "dungeon_run_members",
                schema: "game");

            migrationBuilder.DropTable(
                name: "dungeon_encounters",
                schema: "game");

            migrationBuilder.DropTable(
                name: "dungeon_runs",
                schema: "game");
        }
}
