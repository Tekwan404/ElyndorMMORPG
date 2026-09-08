using Elyndor.Core.Content;
using Elyndor.Core.Talents;
using Elyndor.Infrastructure.Content;

namespace Elyndor.UnitTests.Talents;

public sealed class TalentRuntimeCoverageTests
{
    [Fact]
    public async Task EveryComposedTalentModifierResolvesAtEveryRank()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            RepositoryContentPath());

        foreach (TalentTreeDefinition tree in package.TalentTrees!)
        {
            IReadOnlyDictionary<string, int> selectedRanks = tree.Nodes
                .ToDictionary(node => node.Id, node => node.MaxRank, StringComparer.Ordinal);
            ResolvedTalentModifiers resolved = TalentModifierResolver.Resolve(tree, selectedRanks);
            IReadOnlyList<TalentModifierDefinition> modifiers = tree.Nodes
                .SelectMany(node => node.Modifiers ?? [])
                .ToArray();

            Assert.Empty(resolved.DeferredHooks);
            Assert.Equal(
                modifiers.Count(modifier => modifier.Type == TalentModifierType.EventTriggered),
                resolved.EventHooks.Count);

            foreach (TalentDefinition node in tree.Nodes)
            {
                foreach (TalentModifierDefinition modifier in node.Modifiers ?? [])
                {
                    for (var rank = 1; rank <= node.MaxRank; rank++)
                    {
                        IReadOnlyDictionary<string, int> rankSelection =
                            new Dictionary<string, int>(StringComparer.Ordinal)
                            {
                                [node.Id] = rank
                            };
                        ResolvedTalentModifiers rankResolved =
                            TalentModifierResolver.Resolve(tree, rankSelection);

                        if (modifier.Type == TalentModifierType.EventTriggered)
                        {
                            ResolvedTalentEventHook hook = Assert.Single(
                                rankResolved.EventHooks,
                                item => item.TalentId == node.Id
                                    && item.Key == modifier.Key
                                    && item.TargetId == modifier.TargetId);
                            Assert.Equal(rank, hook.Rank);
                            Assert.Equal(modifier.Values[rank - 1], hook.Value);
                        }
                    }
                }
            }
        }
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
