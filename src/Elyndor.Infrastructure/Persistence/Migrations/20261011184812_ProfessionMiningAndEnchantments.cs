using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elyndor.Infrastructure.Persistence.Migrations;
/// <inheritdoc />
public partial class ProfessionMiningAndEnchantments : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "next_gather_at_utc",
            schema: "game",
            table: "character_professions",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "EnchantmentId",
            schema: "game",
            table: "character_items",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "EnchantmentStatId",
            schema: "game",
            table: "character_items",
            type: "character varying(16)",
            maxLength: 16,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "EnchantmentValue",
            schema: "game",
            table: "character_items",
            type: "numeric(18,4)",
            precision: 18,
            scale: 4,
            nullable: false,
            defaultValue: 0m);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "next_gather_at_utc",
            schema: "game",
            table: "character_professions");

        migrationBuilder.DropColumn(
            name: "EnchantmentId",
            schema: "game",
            table: "character_items");

        migrationBuilder.DropColumn(
            name: "EnchantmentStatId",
            schema: "game",
            table: "character_items");

        migrationBuilder.DropColumn(
            name: "EnchantmentValue",
            schema: "game",
            table: "character_items");
    }
}
