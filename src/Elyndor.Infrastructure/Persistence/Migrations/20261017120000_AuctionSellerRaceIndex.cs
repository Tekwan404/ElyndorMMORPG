using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20261017120000_AuctionSellerRaceIndex")]
public sealed partial class AuctionSellerRaceIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Backfill from immutable character race before enforcing NOT NULL.
        // Keep the whole operation transactional to avoid temporarily exposing unscoped listings.
        migrationBuilder.Sql(
            """
            ALTER TABLE game.auction_listings
                ADD COLUMN "SellerRaceId" character varying(16);

            UPDATE game.auction_listings AS lot
            SET "SellerRaceId" = seller."RaceId"
            FROM game.characters AS seller
            WHERE seller."Id" = lot."SellerId";

            ALTER TABLE game.auction_listings
                ALTER COLUMN "SellerRaceId" SET NOT NULL;

            CREATE INDEX "IX_auction_listings_SellerRaceId_State_ExpiresAt"
                ON game.auction_listings ("SellerRaceId", "State", "ExpiresAt");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP INDEX IF EXISTS game."IX_auction_listings_SellerRaceId_State_ExpiresAt";
            ALTER TABLE game.auction_listings DROP COLUMN "SellerRaceId";
            """);
    }
}
