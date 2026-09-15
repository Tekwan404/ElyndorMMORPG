using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace Elyndor.Infrastructure.Persistence.Migrations;
[DbContext(typeof(GameDbContext))]
[Migration("20260914183000_ArcherTalentReworkV2")]
public partial class ArcherTalentReworkV2 : Migration
{
protected override void Up(MigrationBuilder migrationBuilder)
{
migrationBuilder.Sql(
"""
UPDATE game.character_talent_states AS state
SET "Loadout1RanksJson" = (
SELECT COALESCE(
jsonb_object_agg(
key,
to_jsonb(
CASE key
WHEN 'M-1-1' THEN LEAST((value #>> '{}')::integer, 5)
WHEN 'M-1-2' THEN LEAST((value #>> '{}')::integer, 5)
WHEN 'M-1-3' THEN LEAST((value #>> '{}')::integer, 3)
WHEN 'M-1-4' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'M-2-1' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'M-2-2' THEN LEAST((value #>> '{}')::integer, 3)
WHEN 'M-2-3' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'M-2-4' THEN LEAST((value #>> '{}')::integer, 3)
WHEN 'M-3-1' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'M-3-2' THEN LEAST((value #>> '{}')::integer, 5)
WHEN 'M-3-3' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'M-3-4' THEN LEAST((value #>> '{}')::integer, 3)
WHEN 'M-4-1' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'M-4-2' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'M-4-3' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'M-4-4' THEN LEAST((value #>> '{}')::integer, 3)
WHEN 'M-5-1' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'M-5-2' THEN LEAST((value #>> '{}')::integer, 5)
WHEN 'M-5-3' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'M-5-4' THEN LEAST((value #>> '{}')::integer, 3)
WHEN 'M-6-1' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'M-6-2' THEN LEAST((value #>> '{}')::integer, 3)
WHEN 'M-6-3' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'M-6-4' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'M-7-1' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'M-7-2' THEN LEAST((value #>> '{}')::integer, 3)
WHEN 'M-7-3' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'M-7-4' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'M-8-1' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'M-8-2' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'M-8-3' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'M-9-1' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'B-1-1' THEN LEAST((value #>> '{}')::integer, 5)
WHEN 'B-1-2' THEN LEAST((value #>> '{}')::integer, 5)
WHEN 'B-1-3' THEN LEAST((value #>> '{}')::integer, 5)
WHEN 'B-1-4' THEN LEAST((value #>> '{}')::integer, 3)
WHEN 'B-2-1' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'B-2-2' THEN LEAST((value #>> '{}')::integer, 3)
WHEN 'B-2-3' THEN LEAST((value #>> '{}')::integer, 5)
WHEN 'B-2-4' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'B-3-1' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'B-3-2' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'B-3-3' THEN LEAST((value #>> '{}')::integer, 3)
WHEN 'B-3-4' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'B-4-1' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'B-4-2' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'B-4-3' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'B-4-4' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'B-5-1' THEN LEAST((value #>> '{}')::integer, 5)
WHEN 'B-5-2' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'B-5-3' THEN LEAST((value #>> '{}')::integer, 3)
WHEN 'B-5-4' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'B-6-1' THEN LEAST((value #>> '{}')::integer, 3)
WHEN 'B-6-2' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'B-6-3' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'B-6-4' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'B-7-1' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'B-7-2' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'B-7-3' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'B-7-4' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'B-8-1' THEN LEAST((value #>> '{}')::integer, 3)
WHEN 'B-8-2' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'B-8-3' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'B-9-1' THEN LEAST((value #>> '{}')::integer, 1)
ELSE (value #>> '{}')::integer
END)),
'{}'::jsonb)
FROM jsonb_each(state."Loadout1RanksJson")
WHERE key LIKE 'M-%' OR key LIKE 'B-%'
),
"Loadout2RanksJson" = (
SELECT COALESCE(
jsonb_object_agg(
key,
to_jsonb(
CASE key
WHEN 'M-1-1' THEN LEAST((value #>> '{}')::integer, 5)
WHEN 'M-1-2' THEN LEAST((value #>> '{}')::integer, 5)
WHEN 'M-1-3' THEN LEAST((value #>> '{}')::integer, 3)
WHEN 'M-1-4' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'M-2-1' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'M-2-2' THEN LEAST((value #>> '{}')::integer, 3)
WHEN 'M-2-3' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'M-2-4' THEN LEAST((value #>> '{}')::integer, 3)
WHEN 'M-3-1' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'M-3-2' THEN LEAST((value #>> '{}')::integer, 5)
WHEN 'M-3-3' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'M-3-4' THEN LEAST((value #>> '{}')::integer, 3)
WHEN 'M-4-1' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'M-4-2' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'M-4-3' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'M-4-4' THEN LEAST((value #>> '{}')::integer, 3)
WHEN 'M-5-1' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'M-5-2' THEN LEAST((value #>> '{}')::integer, 5)
WHEN 'M-5-3' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'M-5-4' THEN LEAST((value #>> '{}')::integer, 3)
WHEN 'M-6-1' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'M-6-2' THEN LEAST((value #>> '{}')::integer, 3)
WHEN 'M-6-3' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'M-6-4' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'M-7-1' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'M-7-2' THEN LEAST((value #>> '{}')::integer, 3)
WHEN 'M-7-3' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'M-7-4' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'M-8-1' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'M-8-2' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'M-8-3' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'M-9-1' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'B-1-1' THEN LEAST((value #>> '{}')::integer, 5)
WHEN 'B-1-2' THEN LEAST((value #>> '{}')::integer, 5)
WHEN 'B-1-3' THEN LEAST((value #>> '{}')::integer, 5)
WHEN 'B-1-4' THEN LEAST((value #>> '{}')::integer, 3)
WHEN 'B-2-1' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'B-2-2' THEN LEAST((value #>> '{}')::integer, 3)
WHEN 'B-2-3' THEN LEAST((value #>> '{}')::integer, 5)
WHEN 'B-2-4' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'B-3-1' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'B-3-2' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'B-3-3' THEN LEAST((value #>> '{}')::integer, 3)
WHEN 'B-3-4' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'B-4-1' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'B-4-2' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'B-4-3' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'B-4-4' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'B-5-1' THEN LEAST((value #>> '{}')::integer, 5)
WHEN 'B-5-2' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'B-5-3' THEN LEAST((value #>> '{}')::integer, 3)
WHEN 'B-5-4' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'B-6-1' THEN LEAST((value #>> '{}')::integer, 3)
WHEN 'B-6-2' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'B-6-3' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'B-6-4' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'B-7-1' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'B-7-2' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'B-7-3' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'B-7-4' THEN LEAST((value #>> '{}')::integer, 2)
WHEN 'B-8-1' THEN LEAST((value #>> '{}')::integer, 3)
WHEN 'B-8-2' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'B-8-3' THEN LEAST((value #>> '{}')::integer, 1)
WHEN 'B-9-1' THEN LEAST((value #>> '{}')::integer, 1)
ELSE (value #>> '{}')::integer
END)),
'{}'::jsonb)
FROM jsonb_each(state."Loadout2RanksJson")
WHERE key LIKE 'M-%' OR key LIKE 'B-%'
),
"TalentVersion" = 2,
"StateVersion" = "StateVersion" + 1,
"LastChangedAtUtc" = NOW(),
"LastMutationId" = NULL
WHERE "TalentTreeId" = 'ARCHER_TREE'
AND "TalentVersion" < 2;
""");
}
protected override void Down(MigrationBuilder migrationBuilder)
{
}
}
