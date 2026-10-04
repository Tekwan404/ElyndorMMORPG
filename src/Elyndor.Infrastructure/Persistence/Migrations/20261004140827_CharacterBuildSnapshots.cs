using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;
/// <inheritdoc />
public partial class CharacterBuildSnapshots : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "character_build_archives",
            schema: "game",
            columns: table => new
            {
                BuildHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                CapturedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_character_build_archives", x => x.BuildHash);
            });

        migrationBuilder.CreateTable(
            name: "training_build_references",
            schema: "game",
            columns: table => new
            {
                SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                BuildHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_training_build_references", x => x.SessionId);
                table.ForeignKey(
                    name: "FK_training_build_references_character_build_archives_BuildHash",
                    column: x => x.BuildHash,
                    principalSchema: "game",
                    principalTable: "character_build_archives",
                    principalColumn: "BuildHash",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_training_build_references_BuildHash",
            schema: "game",
            table: "training_build_references",
            column: "BuildHash");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "training_build_references",
            schema: "game");

        migrationBuilder.DropTable(
            name: "character_build_archives",
            schema: "game");
    }
}
