using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations
{
    public partial class ArenaQueueAndMatchmaking : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Mode",
                schema: "game",
                table: "arena_matches",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Ranked");

            migrationBuilder.CreateTable(
                name: "arena_queue_entries",
                schema: "game",
                columns: table => new
                {
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    RatingSnapshot = table.Column<int>(type: "integer", nullable: false),
                    JoinedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_arena_queue_entries", x => x.CharacterId);
                    table.CheckConstraint("ck_arena_queue_level_positive", "\"Level\" > 0");
                    table.CheckConstraint("ck_arena_queue_rating_non_negative", "\"RatingSnapshot\" >= 0");
                    table.ForeignKey(
                        name: "FK_arena_queue_entries_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_arena_queue_mode_joined",
                schema: "game",
                table: "arena_queue_entries",
                columns: new[] { "Mode", "JoinedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_arena_queue_mode_rating_joined",
                schema: "game",
                table: "arena_queue_entries",
                columns: new[] { "Mode", "RatingSnapshot", "JoinedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_arena_matches_active_character_a",
                schema: "game",
                table: "arena_matches",
                columns: new[] { "Outcome", "CharacterAId" });

            migrationBuilder.CreateIndex(
                name: "ix_arena_matches_active_character_b",
                schema: "game",
                table: "arena_matches",
                columns: new[] { "Outcome", "CharacterBId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "arena_queue_entries",
                schema: "game");

            migrationBuilder.DropIndex(
                name: "ix_arena_matches_active_character_a",
                schema: "game",
                table: "arena_matches");

            migrationBuilder.DropIndex(
                name: "ix_arena_matches_active_character_b",
                schema: "game",
                table: "arena_matches");

            migrationBuilder.DropColumn(
                name: "Mode",
                schema: "game",
                table: "arena_matches");
        }
    }
}