using Elyndor.Core.Pvp;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Talents;
using Elyndor.Infrastructure.Content;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaProductionBuildCapabilityTests
{
    private static readonly HashSet<string> PlayableClasses = new(StringComparer.Ordinal)
    {
        "ARCHER", "MAGE", "PALADIN", "WARRIOR"
    };

    [Fact]
    public async Task EveryProductionTalentNodeHasExecutableArenaMechanicsOrExplicitCompanionExclusion()
    {
        var package = await GameContentPackageLoader.LoadAsync(RepositoryContentPath());
        var unsupported = new List<string>();
        Dictionary<string, AbilityDefinition> abilities = (package.Abilities ?? [])
            .ToDictionary(ability => ability.Id, StringComparer.Ordinal);

        foreach (TalentTreeDefinition tree in (package.TalentTrees ?? [])
                     .Where(tree => PlayableClasses.Contains(tree.ClassId))
                     .OrderBy(tree => tree.ClassId, StringComparer.Ordinal))
        {
            foreach (TalentDefinition node in tree.Nodes.OrderBy(node => node.Id, StringComparer.Ordinal))
            {
                ResolvedTalentModifiers resolved = TalentModifierResolver.Resolve(
                    tree,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [node.Id] = node.MaxRank
                    });

                foreach (TalentModifierDefinition deferred in resolved.DeferredHooks)
                {
                    unsupported.Add(
                        $"{tree.ClassId}/{node.BranchId}/{node.Id}: deferred "
                        + $"{deferred.Key}:{deferred.TargetId ?? "<none>"}");
                }

                foreach (ResolvedTalentEventHook hook in ArenaTalentRuntimeSupport
                             .UnsupportedEventHooks(resolved, tree.ClassId)
                             .Where(hook => !ArenaCompanionCapability.RequiresCompanion(hook)))
                {
                    unsupported.Add(
                        $"{tree.ClassId}/{node.BranchId}/{node.Id}: event "
                        + ArenaTalentRuntimeSupport.DescribeUnsupportedHook(hook));
                }

                foreach (string abilityId in resolved.UnlockedAbilityIds
                             .Where(id => !ArenaCompanionCapability.RequiresCompanion(id)))
                {
                    if (!abilities.TryGetValue(abilityId, out AbilityDefinition? ability))
                    {
                        unsupported.Add(
                            $"{tree.ClassId}/{node.BranchId}/{node.Id}: missing unlocked ability {abilityId}");
                        continue;
                    }

                    try
                    {
                        ArenaCombatSession.ValidateAbilities(new Dictionary<string, AbilityDefinition>
                        {
                            [ability.Id] = ArenaTalentRuntimeSupport.ApplyAbilityDefinitionModifiers(
                                ability, resolved)
                        });
                    }
                    catch (NotSupportedException exception)
                    {
                        string shape = $"type={ability.Type}, target={ability.TargetType}, "
                            + $"runtimeParameters={ability.RuntimeParameters?.Count ?? 0}, "
                            + $"actions={string.Join(',', ability.Actions?.Select(action => action.Type) ?? [])}";
                        unsupported.Add(
                            $"{tree.ClassId}/{node.BranchId}/{node.Id}: unsupported unlocked ability "
                            + $"{abilityId} [{shape}] ({exception.Message})");
                    }
                }
            }
        }

        string[] distinctUnsupported = unsupported.Distinct(StringComparer.Ordinal).ToArray();
        string summary = string.Join(", ", distinctUnsupported
            .GroupBy(item => string.Join('/', item.Split('/').Take(2)), StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key}: {group.Count()}"));

        Assert.True(
            unsupported.Count == 0,
            $"Production combat talents would be silently dropped by Arena admission ({distinctUnsupported.Length} entries): {summary}\n"
            + string.Join("\n", distinctUnsupported));
    }

    private static string RepositoryContentPath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "content", "package.json");
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository content package was not found.");
    }
}
