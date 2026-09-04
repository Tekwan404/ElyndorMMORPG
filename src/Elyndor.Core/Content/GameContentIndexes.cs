using System.Runtime.CompilerServices;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Items;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;
using Elyndor.Core.World;

namespace Elyndor.Core.Content;

public sealed class GameContentIndexes
{
    private static readonly ConditionalWeakTable<GameContentPackage, GameContentIndexes> Cache = new();

    private GameContentIndexes(GameContentPackage package)
    {
        DefinitionsByKey = package.Definitions.ToDictionary(
            item => new GameContentDefinitionKey(item.Type, item.Id),
            item => item);
        ClassesById = ToDictionary(package.ClassProfiles, item => item.Id);
        ResourcesById = ToDictionary(package.ResourceProfiles, item => item.Id);
        EffectsById = ToDictionary(package.Effects, item => item.Id);
        AbilitiesById = ToDictionary(package.Abilities, item => item.Id);
        TalentTreesById = ToDictionary(package.TalentTrees, item => item.Id);
        TalentTreesByClassId = ToDictionary(package.TalentTrees, item => item.ClassId);
        ItemsById = ToDictionary(package.Items, item => item.Id);
        EquipmentSetsById = ToDictionary(package.EquipmentSets, item => item.Id);
        MerchantsById = ToDictionary(package.Merchants, item => item.Id);
        LootTablesById = ToDictionary(package.LootTables, item => item.Id);
        MonstersById = ToDictionary(package.Monsters, item => item.Id);
        MonsterAiProfilesById = ToDictionary(package.MonsterAiProfiles, item => item.Id);
        LocationsById = package.Locations.ToDictionary(item => item.Id, StringComparer.Ordinal);
    }

    public IReadOnlyDictionary<GameContentDefinitionKey, GameContentDefinition> DefinitionsByKey { get; }
    public IReadOnlyDictionary<string, ClassProfile> ClassesById { get; }
    public IReadOnlyDictionary<string, ResourceProfile> ResourcesById { get; }
    public IReadOnlyDictionary<string, EffectDefinition> EffectsById { get; }
    public IReadOnlyDictionary<string, AbilityDefinition> AbilitiesById { get; }
    public IReadOnlyDictionary<string, TalentTreeDefinition> TalentTreesById { get; }
    public IReadOnlyDictionary<string, TalentTreeDefinition> TalentTreesByClassId { get; }
    public IReadOnlyDictionary<string, ItemDefinition> ItemsById { get; }
    public IReadOnlyDictionary<string, EquipmentSetDefinition> EquipmentSetsById { get; }
    public IReadOnlyDictionary<string, MerchantDefinition> MerchantsById { get; }
    public IReadOnlyDictionary<string, LootTableDefinition> LootTablesById { get; }
    public IReadOnlyDictionary<string, MonsterDefinition> MonstersById { get; }
    public IReadOnlyDictionary<string, MonsterAiProfile> MonsterAiProfilesById { get; }
    public IReadOnlyDictionary<string, LocationDefinition> LocationsById { get; }

    public static GameContentIndexes For(GameContentPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        return Cache.GetValue(package, static value => new GameContentIndexes(value));
    }

    private static IReadOnlyDictionary<string, T> ToDictionary<T>(
        IReadOnlyList<T>? source,
        Func<T, string> keySelector) =>
        (source ?? []).ToDictionary(keySelector, StringComparer.Ordinal);
}

public readonly record struct GameContentDefinitionKey(string Type, string Id);
