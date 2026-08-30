using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class PhaseOneIdentityWorld : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "game");

        migrationBuilder.CreateTable(
            name: "accounts",
            schema: "game",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LastSeenAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_accounts", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "characters",
            schema: "game",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                CreationRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                NormalizedName = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                RaceId = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                GenderId = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                ClassId = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                Level = table.Column<int>(type: "integer", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_characters", x => x.Id);
                table.ForeignKey(
                    name: "fk_characters_accounts_account_id",
                    column: x => x.AccountId,
                    principalSchema: "game",
                    principalTable: "accounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "character_locations",
            schema: "game",
            columns: table => new
            {
                CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                LocationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_character_locations", x => x.CharacterId);
                table.ForeignKey(
                    name: "fk_character_locations_characters_character_id",
                    column: x => x.CharacterId,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "travel_operations",
            schema: "game",
            columns: table => new
            {
                CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                TargetLocationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ResultLocationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ResultVersion = table.Column<long>(type: "bigint", nullable: false),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_travel_operations", x => new { x.CharacterId, x.RequestId });
                table.ForeignKey(
                    name: "fk_travel_operations_characters_character_id",
                    column: x => x.CharacterId,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "uq_accounts_telegram_user_id",
            schema: "game",
            table: "accounts",
            column: "TelegramUserId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "uq_characters_account_id",
            schema: "game",
            table: "characters",
            column: "AccountId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "uq_characters_creation_request_id",
            schema: "game",
            table: "characters",
            column: "CreationRequestId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "uq_characters_normalized_name",
            schema: "game",
            table: "characters",
            column: "NormalizedName",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "character_locations",
            schema: "game");

        migrationBuilder.DropTable(
            name: "travel_operations",
            schema: "game");

        migrationBuilder.DropTable(
            name: "characters",
            schema: "game");

        migrationBuilder.DropTable(
            name: "accounts",
            schema: "game");
    }
}
