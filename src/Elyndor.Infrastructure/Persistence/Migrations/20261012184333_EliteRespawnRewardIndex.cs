using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

public partial class EliteRespawnRewardIndex : Migration
{
    private static readonly string[] IndexColumns = ["MonsterId", "GrantedAtUtc"];

    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateIndex(
            name: "ix_combat_reward_grants_monster_granted_at",
            schema: "game",
            table: "combat_reward_grants",
            columns: IndexColumns);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropIndex(
            name: "ix_combat_reward_grants_monster_granted_at",
            schema: "game",
            table: "combat_reward_grants");
}
