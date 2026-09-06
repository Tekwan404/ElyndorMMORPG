using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20260906194500_BossUnlockContracts")]
public partial class BossUnlockContracts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "character_contract_completions",
            schema: "game",
            columns: table => new
            {
                CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                ContractId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                TargetMonsterId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                CombatSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "pk_character_contract_completions",
                    x => new { x.CharacterId, x.ContractId });
                table.ForeignKey(
                    name: "fk_character_contract_completions_characters_character_id",
                    column: x => x.CharacterId,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_character_contract_completions_combat_session_id",
            schema: "game",
            table: "character_contract_completions",
            column: "CombatSessionId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "character_contract_completions",
            schema: "game");
    }
}
