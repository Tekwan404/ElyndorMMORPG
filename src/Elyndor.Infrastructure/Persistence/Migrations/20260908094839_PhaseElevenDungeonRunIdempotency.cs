using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

public partial class PhaseElevenDungeonRunIdempotency : Migration
{
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreationRequestId",
                schema: "game",
                table: "dungeon_runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE game.dungeon_runs SET \"CreationRequestId\" = md5(\"Id\"::text)::uuid WHERE \"CreationRequestId\" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreationRequestId",
                schema: "game",
                table: "dungeon_runs",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "uq_dungeon_runs_creation_request_id",
                schema: "game",
                table: "dungeon_runs",
                column: "CreationRequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_dungeon_runs_creation_request_id",
                schema: "game",
                table: "dungeon_runs");

            migrationBuilder.DropColumn(
                name: "CreationRequestId",
                schema: "game",
                table: "dungeon_runs");
        }
}
