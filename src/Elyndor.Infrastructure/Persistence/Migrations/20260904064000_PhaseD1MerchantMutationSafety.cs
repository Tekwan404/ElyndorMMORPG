using System;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20260904064000_PhaseD1MerchantMutationSafety")]
public partial class PhaseD1MerchantMutationSafety : Migration
{
    private static readonly string[] CharacterCommittedAtIndexColumns =
        ["CharacterId", "CommittedAtUtc"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "character_mutations",
            schema: "game",
            columns: table => new
            {
                CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                MutationId = table.Column<Guid>(type: "uuid", nullable: false),
                OperationType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                RequestFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                CommittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_character_mutations", x => new { x.CharacterId, x.MutationId });
                table.ForeignKey(
                    name: "fk_character_mutations_characters_character_id",
                    column: x => x.CharacterId,
                    principalSchema: "game",
                    principalTable: "characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_character_mutations_character_committed_at",
            schema: "game",
            table: "character_mutations",
            columns: CharacterCommittedAtIndexColumns);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "character_mutations", schema: "game");
    }
}
