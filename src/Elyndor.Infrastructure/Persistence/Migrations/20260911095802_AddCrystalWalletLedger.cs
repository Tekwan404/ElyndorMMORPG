using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

    /// <inheritdoc />
    public partial class AddCrystalWalletLedger : Migration
    {
        private static readonly string[] AccountCreatedAtIndexColumns = ["AccountId", "CreatedAtUtc"];
        private static readonly string[] AccountOperationIndexColumns = ["AccountId", "OperationId"];
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "crystal_ledger_entries",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryType = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Delta = table.Column<long>(type: "bigint", nullable: false),
                    BalanceAfter = table.Column<long>(type: "bigint", nullable: false),
                    Reference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_crystal_ledger_entries", x => x.Id);
                    table.CheckConstraint("ck_crystal_ledger_entries_balance_after_non_negative", "\"BalanceAfter\" >= 0");
                    table.CheckConstraint("ck_crystal_ledger_entries_delta_non_zero", "\"Delta\" <> 0");
                    table.ForeignKey(
                        name: "fk_crystal_ledger_entries_accounts_account_id",
                        column: x => x.AccountId,
                        principalSchema: "game",
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "crystal_wallets",
                schema: "game",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Balance = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_crystal_wallets", x => x.AccountId);
                    table.CheckConstraint("ck_crystal_wallets_balance_non_negative", "\"Balance\" >= 0");
                    table.ForeignKey(
                        name: "fk_crystal_wallets_accounts_account_id",
                        column: x => x.AccountId,
                        principalSchema: "game",
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_crystal_ledger_entries_account_created_at",
                schema: "game",
                table: "crystal_ledger_entries",
                columns: AccountCreatedAtIndexColumns);

            migrationBuilder.CreateIndex(
                name: "uq_crystal_ledger_entries_account_operation",
                schema: "game",
                table: "crystal_ledger_entries",
                columns: AccountOperationIndexColumns,
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "crystal_ledger_entries",
                schema: "game");

            migrationBuilder.DropTable(
                name: "crystal_wallets",
                schema: "game");
        }
    }
