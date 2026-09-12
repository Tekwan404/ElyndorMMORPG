using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

    /// <inheritdoc />
    public partial class AddAfkFarmIntervalGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "afk_farm_interval_grants",
                schema: "game",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IntervalIndex = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Kills = table.Column<int>(type: "integer", nullable: false),
                    XpEarned = table.Column<int>(type: "integer", nullable: false),
                    GoldEarned = table.Column<int>(type: "integer", nullable: false),
                    LootJson = table.Column<string>(type: "jsonb", nullable: false),
                    GrantedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_afk_farm_interval_grants", x => new { x.SessionId, x.IntervalIndex });
                    table.CheckConstraint("ck_afk_farm_interval_grants_loot_json", "jsonb_typeof(\"LootJson\") = 'array'");
                    table.CheckConstraint("ck_afk_farm_interval_grants_non_negative_rewards", "\"Kills\" >= 0 AND \"XpEarned\" >= 0 AND \"GoldEarned\" >= 0");
                    table.CheckConstraint("ck_afk_farm_interval_grants_positive_window", "\"EndedAtUtc\" > \"StartedAtUtc\"");
                    table.ForeignKey(
                        name: "fk_afk_farm_interval_grants_sessions_session_id",
                        column: x => x.SessionId,
                        principalSchema: "game",
                        principalTable: "afk_farm_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "afk_farm_interval_grants",
                schema: "game");
        }
    }
