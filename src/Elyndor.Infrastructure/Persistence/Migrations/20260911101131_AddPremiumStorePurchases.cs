using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

    /// <inheritdoc />
    public partial class AddPremiumStorePurchases : Migration
    {
        private static readonly string[] AccountSkuIndexColumns = ["AccountId", "Sku"];
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "premium_store_purchases",
                schema: "game",
                columns: table => new
                {
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ItemDefinitionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    CrystalPrice = table.Column<long>(type: "bigint", nullable: false),
                    PurchasedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_premium_store_purchases", x => x.OperationId);
                    table.CheckConstraint("ck_premium_store_purchases_price_positive", "\"CrystalPrice\" > 0");
                    table.CheckConstraint("ck_premium_store_purchases_quantity_positive", "\"Quantity\" > 0");
                });

            migrationBuilder.CreateIndex(
                name: "ix_premium_store_purchases_account_sku",
                schema: "game",
                table: "premium_store_purchases",
                columns: AccountSkuIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "premium_store_purchases",
                schema: "game");
        }
    }
