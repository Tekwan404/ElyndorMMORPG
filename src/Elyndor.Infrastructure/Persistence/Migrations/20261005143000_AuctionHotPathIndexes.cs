using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20261005143000_AuctionHotPathIndexes")]
public sealed partial class AuctionHotPathIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE INDEX IF NOT EXISTS ix_auction_listings_active_seller_id
            ON game.auction_listings ("SellerId")
            WHERE "State" = 'ACTIVE';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP INDEX IF EXISTS game.ix_auction_listings_active_seller_id;
            """);
    }
}
