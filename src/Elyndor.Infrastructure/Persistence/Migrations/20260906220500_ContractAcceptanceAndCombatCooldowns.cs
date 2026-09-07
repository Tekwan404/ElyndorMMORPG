using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20260906220500_ContractAcceptanceAndCombatCooldowns")]
public partial class ContractAcceptanceAndCombatCooldowns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "character_ability_cooldowns",
            schema: "game",
            columns: table => new
            {
                CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                AbilityId = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: false),
                ReadyAtUtc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "pk_character_ability_cooldowns",
                    x => new { x.CharacterId, x.AbilityId });
                table.ForeignKey(
                    name: "fk_character_ability_cooldowns_characters_character_id",
                    column: x => x.CharacterId,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "character_contract_acceptances",
            schema: "game",
            columns: table => new
            {
                CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                ContractId = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: false),
                AcceptedAtUtc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "pk_character_contract_acceptances",
                    x => new { x.CharacterId, x.ContractId });
                table.ForeignKey(
                    name: "fk_character_contract_acceptances_characters_character_id",
                    column: x => x.CharacterId,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_character_ability_cooldowns_ready_at_utc",
            schema: "game",
            table: "character_ability_cooldowns",
            column: "ReadyAtUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "character_ability_cooldowns",
            schema: "game");

        migrationBuilder.DropTable(
            name: "character_contract_acceptances",
            schema: "game");
    }
}
