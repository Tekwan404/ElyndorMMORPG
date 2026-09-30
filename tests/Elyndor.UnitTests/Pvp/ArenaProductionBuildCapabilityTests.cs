using Elyndor.Core.Pvp;
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
    public async Task EveryProductionTalentNodeHasArenaRuntimeSemantics()
    {
        var package = await GameContentPackageLoader.LoadAsync(RepositoryContentPath());
        var unsupported = new List<string>();

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

                foreach (ResolvedTalentEventHook hook in ArenaTalentRuntimeSupport.UnsupportedEventHooks(resolved))
                {
                    unsupported.Add(
                        $"{tree.ClassId}/{node.BranchId}/{node.Id}: event "
                        + ArenaTalentRuntimeSupport.DescribeUnsupportedHook(hook));
                }
            }
        }

        Assert.True(
            unsupported.Count == 0,
            "Current production talent mechanics without Arena semantics:\n"
            + string.Join("\n", unsupported.Distinct(StringComparer.Ordinal)));
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
