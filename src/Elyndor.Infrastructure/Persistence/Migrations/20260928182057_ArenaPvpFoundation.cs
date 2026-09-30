using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable IDE0161, CA1861 // EF Core generated migration follows the repository's migration format.

namespace Elyndor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ArenaPvpFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "arena_honor_wallets",
                schema: "game",
                columns: table => new
                {
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Balance = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_arena_honor_wallets", x => x.CharacterId);
                    table.CheckConstraint("ck_arena_honor_wallets_balance_non_negative", "\"Balance\" >= 0");
                    table.ForeignKey(
                        name: "FK_arena_honor_wallets_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "arena_matches",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterAId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterBId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonId = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SettledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    EligibleForProgression = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_arena_matches", x => x.Id);
                    table.CheckConstraint("ck_arena_matches_distinct_characters", "\"CharacterAId\" <> \"CharacterBId\"");
                    table.ForeignKey(
                        name: "FK_arena_matches_characters_CharacterAId",
                        column: x => x.CharacterAId,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_arena_matches_characters_CharacterBId",
                        column: x => x.CharacterBId,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "arena_standings",
                schema: "game",
                columns: table => new
                {
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonId = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Wins = table.Column<int>(type: "integer", nullable: false),
                    Losses = table.Column<int>(type: "integer", nullable: false),
                    Draws = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_arena_standings", x => new { x.CharacterId, x.SeasonId });
                    table.CheckConstraint("ck_arena_standings_counts_non_negative", "\"Wins\" >= 0 AND \"Losses\" >= 0 AND \"Draws\" >= 0");
                    table.CheckConstraint("ck_arena_standings_rating_non_negative", "\"Rating\" >= 0");
                    table.ForeignKey(
                        name: "FK_arena_standings_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "arena_honor_ledger_entries",
                schema: "game",
                columns: table => new
                {
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Delta = table.Column<long>(type: "bigint", nullable: false),
                    BalanceAfter = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_arena_honor_ledger_entries", x => new { x.MatchId, x.CharacterId });
                    table.CheckConstraint("ck_arena_honor_ledger_balance_valid", "\"BalanceAfter\" >= \"Delta\"");
                    table.CheckConstraint("ck_arena_honor_ledger_delta_positive", "\"Delta\" > 0");
                    table.ForeignKey(
                        name: "FK_arena_honor_ledger_entries_arena_matches_MatchId",
                        column: x => x.MatchId,
                        principalSchema: "game",
                        principalTable: "arena_matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_arena_honor_ledger_entries_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_arena_honor_ledger_character_created",
                schema: "game",
                table: "arena_honor_ledger_entries",
                columns: new[] { "CharacterId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_arena_matches_character_a",
                schema: "game",
                table: "arena_matches",
                column: "CharacterAId");

            migrationBuilder.CreateIndex(
                name: "ix_arena_matches_character_b",
                schema: "game",
                table: "arena_matches",
                column: "CharacterBId");

            migrationBuilder.CreateIndex(
                name: "ix_arena_standings_leaderboard",
                schema: "game",
                table: "arena_standings",
                columns: new[] { "SeasonId", "Rating", "Wins" },
                descending: new[] { false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "arena_honor_ledger_entries",
                schema: "game");

            migrationBuilder.DropTable(
                name: "arena_honor_wallets",
                schema: "game");

            migrationBuilder.DropTable(
                name: "arena_standings",
                schema: "game");

            migrationBuilder.DropTable(
                name: "arena_matches",
                schema: "game");
        }
    }
}
