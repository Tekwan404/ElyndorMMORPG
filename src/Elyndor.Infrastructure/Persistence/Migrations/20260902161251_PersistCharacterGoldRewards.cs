using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

    /// <inheritdoc />
    public partial class PersistCharacterGoldRewards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GoldEarned",
                schema: "game",
                table: "combat_reward_grants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "Gold",
                schema: "game",
                table: "characters",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddCheckConstraint(
                name: "ck_combat_reward_grants_gold_non_negative",
                schema: "game",
                table: "combat_reward_grants",
                sql: "\"GoldEarned\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_characters_gold_non_negative",
                schema: "game",
                table: "characters",
                sql: "\"Gold\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_combat_reward_grants_gold_non_negative",
                schema: "game",
                table: "combat_reward_grants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_characters_gold_non_negative",
                schema: "game",
                table: "characters");

            migrationBuilder.DropColumn(
                name: "GoldEarned",
                schema: "game",
                table: "combat_reward_grants");

            migrationBuilder.DropColumn(
                name: "Gold",
                schema: "game",
                table: "characters");
        }
}
