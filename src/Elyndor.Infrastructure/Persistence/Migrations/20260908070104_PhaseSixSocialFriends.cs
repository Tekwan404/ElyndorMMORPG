using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class PhaseSixSocialFriends : Migration
{
    private static readonly string[] PendingPairIndexColumns = ["PairKey", "Status"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
            migrationBuilder.AddColumn<string>(
                name: "PublicCode",
                schema: "game",
                table: "characters",
                type: "character varying(14)",
                maxLength: 14,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                "WITH codes AS (SELECT \"Id\", row_number() OVER (ORDER BY \"Id\") AS number FROM game.characters) "
                + "UPDATE game.characters AS character "
                + "SET \"PublicCode\" = 'ELY-' || upper(lpad(to_hex(codes.number), 10, '0')) "
                + "FROM codes WHERE character.\"Id\" = codes.\"Id\";" );

            migrationBuilder.AlterColumn<string>(
                name: "PublicCode",
                schema: "game",
                table: "characters",
                type: "character varying(14)",
                maxLength: 14,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(14)",
                oldMaxLength: 14,
                oldDefaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedTelegramUsername",
                schema: "game",
                table: "accounts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelegramUsername",
                schema: "game",
                table: "accounts",
                type: "character varying(33)",
                maxLength: 33,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "friend_requests",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    PairKey = table.Column<string>(type: "character varying(73)", maxLength: 73, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecidedByCharacterId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_friend_requests", x => x.Id);
                    table.ForeignKey(
                        name: "fk_friend_requests_requester_character",
                        column: x => x.RequesterCharacterId,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_friend_requests_target_character",
                        column: x => x.TargetCharacterId,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "friendships",
                schema: "game",
                columns: table => new
                {
                    PairKey = table.Column<string>(type: "character varying(73)", maxLength: 73, nullable: false),
                    CharacterAId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterBId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_friendships", x => x.PairKey);
                    table.ForeignKey(
                        name: "fk_friendships_character_a",
                        column: x => x.CharacterAId,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_friendships_character_b",
                        column: x => x.CharacterBId,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "uq_characters_public_code",
                schema: "game",
                table: "characters",
                column: "PublicCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_accounts_normalized_telegram_username",
                schema: "game",
                table: "accounts",
                column: "NormalizedTelegramUsername",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_friend_requests_RequesterCharacterId",
                schema: "game",
                table: "friend_requests",
                column: "RequesterCharacterId");

            migrationBuilder.CreateIndex(
                name: "ix_friend_requests_target_character_id",
                schema: "game",
                table: "friend_requests",
                column: "TargetCharacterId");

        migrationBuilder.CreateIndex(
                name: "uq_friend_requests_pending_pair",
                schema: "game",
                table: "friend_requests",
                columns: PendingPairIndexColumns,
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "ix_friendships_character_a_id",
                schema: "game",
                table: "friendships",
                column: "CharacterAId");

            migrationBuilder.CreateIndex(
                name: "ix_friendships_character_b_id",
                schema: "game",
                table: "friendships",
                column: "CharacterBId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
            migrationBuilder.DropTable(
                name: "friend_requests",
                schema: "game");

            migrationBuilder.DropTable(
                name: "friendships",
                schema: "game");

            migrationBuilder.DropIndex(
                name: "uq_characters_public_code",
                schema: "game",
                table: "characters");

            migrationBuilder.DropIndex(
                name: "uq_accounts_normalized_telegram_username",
                schema: "game",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "PublicCode",
                schema: "game",
                table: "characters");

            migrationBuilder.DropColumn(
                name: "NormalizedTelegramUsername",
                schema: "game",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "TelegramUsername",
                schema: "game",
                table: "accounts");
    }
}
