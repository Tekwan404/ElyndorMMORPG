using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class PremiumStoreBulkPurchase : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "PackCount",
            schema: "game",
            table: "premium_store_purchases",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddCheckConstraint(
            name: "ck_premium_store_purchases_pack_count_positive",
            schema: "game",
            table: "premium_store_purchases",
            sql: "\"PackCount\" > 0");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_premium_store_purchases_pack_count_positive",
            schema: "game",
            table: "premium_store_purchases");

        migrationBuilder.DropColumn(
            name: "PackCount",
            schema: "game",
            table: "premium_store_purchases");
    }
}
