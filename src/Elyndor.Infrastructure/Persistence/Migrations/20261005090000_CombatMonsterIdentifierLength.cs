using Microsoft.EntityFrameworkCore.Migrations;

namespace Elyndor.Infrastructure.Persistence.Migrations;

public sealed partial class CombatMonsterIdentifierLength : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(name: "MonsterId", schema: "game", table: "combat_reward_grants",
            type: "character varying(128)", maxLength: 128, nullable: false,
            oldClrType: typeof(string), oldType: "character varying(64)", oldMaxLength: 64);
        migrationBuilder.AlterColumn<string>(name: "MonsterId", schema: "game", table: "dungeon_encounters",
            type: "character varying(128)", maxLength: 128, nullable: false,
            oldClrType: typeof(string), oldType: "character varying(64)", oldMaxLength: 64);
        migrationBuilder.AlterColumn<string>(name: "TargetMonsterId", schema: "game", table: "character_contract_completions",
            type: "character varying(128)", maxLength: 128, nullable: false,
            oldClrType: typeof(string), oldType: "character varying(64)", oldMaxLength: 64);
        migrationBuilder.AlterColumn<string>(name: "TargetMonsterId", schema: "game", table: "afk_farm_sessions",
            type: "character varying(128)", maxLength: 128, nullable: true,
            oldClrType: typeof(string), oldType: "character varying(64)", oldMaxLength: 64, oldNullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM game.combat_reward_grants WHERE length("MonsterId") > 64)
                    OR EXISTS (SELECT 1 FROM game.dungeon_encounters WHERE length("MonsterId") > 64)
                    OR EXISTS (SELECT 1 FROM game.character_contract_completions WHERE length("TargetMonsterId") > 64)
                    OR EXISTS (SELECT 1 FROM game.afk_farm_sessions WHERE length("TargetMonsterId") > 64) THEN
                    RAISE EXCEPTION 'Cannot narrow monster identifiers while ids longer than 64 remain';
                END IF;
            END $$;
            """);
        migrationBuilder.AlterColumn<string>(name: "MonsterId", schema: "game", table: "combat_reward_grants",
            type: "character varying(64)", maxLength: 64, nullable: false,
            oldClrType: typeof(string), oldType: "character varying(128)", oldMaxLength: 128);
        migrationBuilder.AlterColumn<string>(name: "MonsterId", schema: "game", table: "dungeon_encounters",
            type: "character varying(64)", maxLength: 64, nullable: false,
            oldClrType: typeof(string), oldType: "character varying(128)", oldMaxLength: 128);
        migrationBuilder.AlterColumn<string>(name: "TargetMonsterId", schema: "game", table: "character_contract_completions",
            type: "character varying(64)", maxLength: 64, nullable: false,
            oldClrType: typeof(string), oldType: "character varying(128)", oldMaxLength: 128);
        migrationBuilder.AlterColumn<string>(name: "TargetMonsterId", schema: "game", table: "afk_farm_sessions",
            type: "character varying(64)", maxLength: 64, nullable: true,
            oldClrType: typeof(string), oldType: "character varying(128)", oldMaxLength: 128, oldNullable: true);
    }
}
