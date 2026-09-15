using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

    /// <inheritdoc />
    public partial class AddReleaseAdminNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "release_admin_notifications",
                schema: "game",
                columns: table => new
                {
                    ReleaseId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_release_admin_notifications", x => new { x.ReleaseId, x.TelegramUserId });
                });

            migrationBuilder.CreateIndex(
                name: "ix_release_admin_notifications_sent_at_utc",
                schema: "game",
                table: "release_admin_notifications",
                column: "SentAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "release_admin_notifications",
                schema: "game");
        }
    }
