using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

    /// <inheritdoc />
public partial class AddPromoCodeRedemptions : Migration
    {
        private static readonly string[] AccountCodeColumns = ["AccountId", "Code"];
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "promo_code_redemptions",
                schema: "game",
                columns: table => new
                {
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RedeemedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_promo_code_redemptions", x => x.OperationId);
                    table.ForeignKey(
                        name: "fk_promo_code_redemptions_accounts_account_id",
                        column: x => x.AccountId,
                        principalSchema: "game",
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_promo_code_redemptions_characters_character_id",
                        column: x => x.CharacterId,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_promo_code_redemptions_account_code",
                schema: "game",
                table: "promo_code_redemptions",
                columns: AccountCodeColumns);

            migrationBuilder.CreateIndex(
                name: "IX_promo_code_redemptions_CharacterId",
                schema: "game",
                table: "promo_code_redemptions",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "ix_promo_code_redemptions_code",
                schema: "game",
                table: "promo_code_redemptions",
                column: "Code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "promo_code_redemptions",
                schema: "game");
        }
    }
