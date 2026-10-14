using Elyndor.Contracts.Dungeons;
using Elyndor.Core.Content;
using Elyndor.Core.Dungeons;
using Elyndor.Server.World;

namespace Elyndor.Server.Dungeons;

internal static class DungeonPreviewMapper
{
    public static DungeonPreviewResponse ToResponse(DungeonDefinition definition, GameContentSnapshot content)
    {
        var floors = DungeonFloorLayout.Build(definition).Select(floor =>
        {
            var encounters = definition.Encounters.Skip(floor.FirstEncounterIndex)
                .Take(floor.LastEncounterIndex - floor.FirstEncounterIndex + 1).ToArray();
            var boss = encounters.LastOrDefault(encounter => encounter.IsBoss);
            string? bossName = boss is null ? null : MonsterName(boss.MonsterId, content);
            return new DungeonFloorPreviewResponse(floor.Number, floor.FirstEncounterIndex, floor.LastEncounterIndex,
                bossName, WorldEndpoints.BuildMonsterLoot(MonsterIds(encounters), content.Indexes),
                floor.DisplayName, floor.RequiredEncounterId);
        }).ToArray();
        return new DungeonPreviewResponse(definition.Id, definition.DisplayName, definition.Description,
            definition.MinimumLevel, definition.MaximumLevel, definition.EntryLocationId,
            definition.MinimumPartySize, definition.MaximumPartySize,
            definition.Encounters.Select(encounter => new DungeonEncounterPreviewResponse(encounter.Id,
                encounter.MonsterId, encounter.CheckpointId, encounter.IsBoss,
                MonsterName(encounter.MonsterId, content))).ToArray(),
            floors, WorldEndpoints.BuildMonsterLoot(MonsterIds(definition.Encounters), content.Indexes));
    }

    private static string? MonsterName(string monsterId, GameContentSnapshot content)
    {
        var monster = content.Indexes.MonstersById.GetValueOrDefault(monsterId);
        return monster?.DisplayName ?? monster?.Name;
    }

    private static IEnumerable<string> MonsterIds(IEnumerable<DungeonEncounterDefinition> encounters) =>
        encounters.SelectMany(encounter => new[] { encounter.MonsterId }
            .Concat((encounter.Adds ?? []).Select(add => add.MonsterId))).Distinct(StringComparer.Ordinal);
}
