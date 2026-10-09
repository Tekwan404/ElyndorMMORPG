using Elyndor.Core.Content;

namespace Elyndor.Infrastructure.Professions;

public static class ProfessionWorkshopCatalog
{
    public static IReadOnlyList<ProfessionMaterialSourceState> MaterialSources(GameContentPackage package)
    {
        var sources = (package.SkinningSources ?? []).GroupBy(source => source.MonsterId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var items = (package.Items ?? []).ToDictionary(item => item.Id, StringComparer.Ordinal);
        var monsters = (package.Monsters ?? []).ToDictionary(monster => monster.Id, StringComparer.Ordinal);
        return package.Locations.SelectMany(location => (location.Encounters ?? [])
                .Where(encounter => sources.ContainsKey(encounter.MonsterId))
                .Select(encounter =>
                {
                    var source = sources[encounter.MonsterId];
                    return new ProfessionMaterialSourceState(source.ItemId, items[source.ItemId].Name,
                        monsters[encounter.MonsterId].Name, location.Id, location.DisplayName, source.RequiredSkill, source.SkillUpUntil);
                }))
            .Distinct()
            .OrderBy(source => source.RequiredSkill)
            .ThenBy(source => source.MonsterName, StringComparer.Ordinal)
            .ToArray();
    }
}
