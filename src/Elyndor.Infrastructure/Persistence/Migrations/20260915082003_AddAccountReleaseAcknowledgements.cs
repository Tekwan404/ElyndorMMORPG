using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

    /// <inheritdoc />
    public partial class AddAccountReleaseAcknowledgements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account_release_acknowledgements",
                schema: "game",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReleaseId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_account_release_acknowledgements", x => new { x.AccountId, x.ReleaseId });
                    table.ForeignKey(
                        name: "fk_account_release_acknowledgements_accounts_account_id",
                        column: x => x.AccountId,
                        principalSchema: "game",
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_account_release_acknowledgements_acknowledged_at_utc",
                schema: "game",
                table: "account_release_acknowledgements",
                column: "AcknowledgedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_release_acknowledgements",
                schema: "game");
        }
    }
