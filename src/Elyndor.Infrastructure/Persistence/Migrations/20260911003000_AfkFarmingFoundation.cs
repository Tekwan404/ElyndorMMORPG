using System;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20260911003000_AfkFarmingFoundation")]
public partial class AfkFarmingFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "afk_farm_sessions",
            schema: "game",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                LocationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                EndsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LastProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                ContentVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                BalanceVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                CharacterSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                StopReason = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                Version = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_afk_farm_sessions", x => x.Id);
                table.ForeignKey(
                    name: "fk_afk_farm_sessions_characters_character_id",
                    column: x => x.CharacterId,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.CheckConstraint(
                    name: "ck_afk_farm_sessions_positive_duration",
                    sql: "\"EndsAtUtc\" > \"StartedAtUtc\"");
                table.CheckConstraint(
                    name: "ck_afk_farm_sessions_processing_window",
                    sql: "\"LastProcessedAtUtc\" >= \"StartedAtUtc\" AND \"LastProcessedAtUtc\" <= \"EndsAtUtc\"");
            });

        migrationBuilder.CreateIndex(
            name: "ix_afk_farm_sessions_character_created_at_utc",
            schema: "game",
            table: "afk_farm_sessions",
            columns: new[] { "CharacterId", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "ix_afk_farm_sessions_status_ends_at_utc",
            schema: "game",
            table: "afk_farm_sessions",
            columns: new[] { "Status", "EndsAtUtc" });

        migrationBuilder.CreateIndex(
            name: "uq_afk_farm_sessions_active_character_id",
            schema: "game",
            table: "afk_farm_sessions",
            column: "CharacterId",
            unique: true,
            filter: "\"Status\" = 'Active'");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "afk_farm_sessions",
            schema: "game");
    }
}
