using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20260906180500_MultiEnemyCombatRewards")]
public partial class MultiEnemyCombatRewards : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "RewardSourcesJson",
            schema: "game",
            table: "combat_reward_grants",
            type: "jsonb",
            nullable: false,
            defaultValueSql: "'[]'::jsonb");

        migrationBuilder.AddCheckConstraint(
            name: "ck_combat_reward_grants_sources_json",
            schema: "game",
            table: "combat_reward_grants",
            sql: "jsonb_typeof(\"RewardSourcesJson\") = 'array'");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_combat_reward_grants_sources_json",
            schema: "game",
            table: "combat_reward_grants");

        migrationBuilder.DropColumn(
            name: "RewardSourcesJson",
            schema: "game",
            table: "combat_reward_grants");
    }
}
