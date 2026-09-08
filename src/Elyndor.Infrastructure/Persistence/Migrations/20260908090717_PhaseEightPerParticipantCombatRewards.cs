using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;
    /// <inheritdoc />
public partial class PhaseEightPerParticipantCombatRewards : Migration
{
    private static readonly string[] SessionCharacterColumns = ["CombatSessionId", "CharacterId"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_combat_reward_grants",
                schema: "game",
                table: "combat_reward_grants");

            migrationBuilder.AddPrimaryKey(
                name: "pk_combat_reward_grants",
                schema: "game",
                table: "combat_reward_grants",
                columns: SessionCharacterColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_combat_reward_grants",
                schema: "game",
                table: "combat_reward_grants");

            migrationBuilder.AddPrimaryKey(
                name: "pk_combat_reward_grants",
                schema: "game",
                table: "combat_reward_grants",
                column: "CombatSessionId");
        }
    }
