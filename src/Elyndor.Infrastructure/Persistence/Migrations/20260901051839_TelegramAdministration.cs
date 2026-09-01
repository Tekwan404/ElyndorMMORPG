using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class TelegramAdministration : Migration
{
        private static readonly string[] AdministratorIndexColumns =
            ["AdministratorTelegramUserId", "ReceivedAtUtc"];
        private static readonly string[] TargetIndexColumns =
            ["TargetTelegramUserId", "ReceivedAtUtc"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_command_audits",
                schema: "game",
                columns: table => new
                {
                    UpdateId = table.Column<long>(type: "bigint", nullable: false),
                    AdministratorTelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    CommandName = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetTelegramUserId = table.Column<long>(type: "bigint", nullable: true),
                    ResultCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResultSummary = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admin_command_audits", x => x.UpdateId);
                });

            migrationBuilder.CreateIndex(
                name: "ix_admin_command_audits_administrator_received_at",
                schema: "game",
                table: "admin_command_audits",
                columns: AdministratorIndexColumns);

            migrationBuilder.CreateIndex(
                name: "ix_admin_command_audits_target_received_at",
                schema: "game",
                table: "admin_command_audits",
                columns: TargetIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_command_audits",
                schema: "game");
        }
}
