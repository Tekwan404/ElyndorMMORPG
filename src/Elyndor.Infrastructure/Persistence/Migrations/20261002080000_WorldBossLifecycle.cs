using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20261002080000_WorldBossLifecycle")]
public partial class WorldBossLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "uq_world_boss_spawns_single_active",
            schema: "game",
            table: "world_boss_spawns",
            column: "Status",
            unique: true,
            filter: "\"Status\" = 'Active'");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "uq_world_boss_spawns_single_active",
            schema: "game",
            table: "world_boss_spawns");
    }
}
