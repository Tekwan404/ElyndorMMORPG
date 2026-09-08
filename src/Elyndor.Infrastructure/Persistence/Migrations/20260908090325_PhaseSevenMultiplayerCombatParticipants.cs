using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;
    /// <inheritdoc />
public partial class PhaseSevenMultiplayerCombatParticipants : Migration
{
    private static readonly string[] SessionCharacterColumns = ["SessionId", "CharacterId"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_combat_consumable_uses_active_combat_session_id",
                schema: "game",
                table: "combat_consumable_uses");

            migrationBuilder.DropPrimaryKey(
                name: "pk_active_combat_sessions",
                schema: "game",
                table: "active_combat_sessions");

            migrationBuilder.AddPrimaryKey(
                name: "pk_active_combat_sessions",
                schema: "game",
                table: "active_combat_sessions",
                columns: SessionCharacterColumns);

            migrationBuilder.CreateIndex(
                name: "IX_combat_consumable_uses_SessionId_CharacterId",
                schema: "game",
                table: "combat_consumable_uses",
                columns: SessionCharacterColumns);

            migrationBuilder.CreateIndex(
                name: "ix_active_combat_sessions_session_id",
                schema: "game",
                table: "active_combat_sessions",
                column: "SessionId");

            migrationBuilder.AddForeignKey(
                name: "fk_combat_consumable_uses_active_combat_session",
                schema: "game",
                table: "combat_consumable_uses",
                columns: SessionCharacterColumns,
                principalSchema: "game",
                principalTable: "active_combat_sessions",
                principalColumns: SessionCharacterColumns,
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_combat_consumable_uses_active_combat_session",
                schema: "game",
                table: "combat_consumable_uses");

            migrationBuilder.DropIndex(
                name: "IX_combat_consumable_uses_SessionId_CharacterId",
                schema: "game",
                table: "combat_consumable_uses");

            migrationBuilder.DropPrimaryKey(
                name: "pk_active_combat_sessions",
                schema: "game",
                table: "active_combat_sessions");

            migrationBuilder.DropIndex(
                name: "ix_active_combat_sessions_session_id",
                schema: "game",
                table: "active_combat_sessions");

            migrationBuilder.AddPrimaryKey(
                name: "pk_active_combat_sessions",
                schema: "game",
                table: "active_combat_sessions",
                column: "SessionId");

            migrationBuilder.AddForeignKey(
                name: "fk_combat_consumable_uses_active_combat_session_id",
                schema: "game",
                table: "combat_consumable_uses",
                column: "SessionId",
                principalSchema: "game",
                principalTable: "active_combat_sessions",
                principalColumn: "SessionId",
                onDelete: ReferentialAction.Cascade);
        }
    }
