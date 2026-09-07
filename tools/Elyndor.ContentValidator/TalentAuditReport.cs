using Elyndor.Core.Content;
using Elyndor.Core.Talents;

namespace Elyndor.ContentValidator;

public sealed record TalentAuditReport(
    int TreeCount,
    int BranchCount,
    int NodeCount,
    int ModifierCount,
    int DeferredModifierCount,
    int FullyDeferredNodeCount,
    int MissingRussianTextCount,
    IReadOnlyList<TalentAuditEntry> Entries)
{
    public static TalentAuditReport Create(GameContentPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        List<TalentAuditEntry> entries = [];
        int branchCount = 0;
        int deferredModifierCount = 0;
        int fullyDeferredNodeCount = 0;
        int missingRussianTextCount = 0;

        foreach (TalentTreeDefinition tree in (package.TalentTrees ?? [])
                     .OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            branchCount += tree.Branches.Count;

            foreach (TalentDefinition node in tree.Nodes
                         .OrderBy(item => item.BranchId, StringComparer.Ordinal)
                         .ThenBy(item => item.Id, StringComparer.Ordinal))
            {
                IReadOnlyList<TalentModifierDefinition> modifiers = node.Modifiers ?? [];
                if (modifiers.Count > 0 && modifiers.All(IsDeferred))
                    fullyDeferredNodeCount++;

                if (!ContainsRussian(node.Name) || !ContainsRussian(node.Description))
                    missingRussianTextCount++;

                List<TalentAuditModifier> auditModifiers = [];
                for (var index = 0; index < modifiers.Count; index++)
                {
                    TalentModifierDefinition modifier = modifiers[index];
                    if (IsDeferred(modifier))
                        deferredModifierCount++;

                    List<TalentAuditRank> ranks = [];
                    for (var rank = 0; rank < modifier.Values.Count; rank++)
                    {
                        decimal? secondaryValue = modifier.SecondaryValues is { Count: > 0 }
                            && modifier.SecondaryValues.Count > rank
                            ? modifier.SecondaryValues[rank]
                            : null;
                        ranks.Add(new(rank + 1, modifier.Values[rank], secondaryValue));
                    }

                    auditModifiers.Add(new(
                        $"{tree.Id}:{node.BranchId}:{node.Id}:{index + 1}",
                        modifier.Type,
                        modifier.Key,
                        modifier.TargetId,
                        modifier.RuntimeStatus,
                        modifier.DeferredOwner,
                        ranks));
                }

                entries.Add(new(
                    tree.Id,
                    tree.ClassId,
                    node.BranchId,
                    node.Id,
                    node.Name,
                    node.Description,
                    node.MaxRank,
                    auditModifiers));
            }
        }

        return new(
            (package.TalentTrees ?? []).Count,
            branchCount,
            entries.Count,
            entries.Sum(entry => entry.Modifiers.Count),
            deferredModifierCount,
            fullyDeferredNodeCount,
            missingRussianTextCount,
            entries);
    }

    private static bool IsDeferred(TalentModifierDefinition modifier) =>
        modifier.RuntimeStatus == TalentModifierRuntimeStatus.Deferred;

    private static bool ContainsRussian(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Any(character => character is >= '\u0400' and <= '\u04FF');
}

public sealed record TalentAuditEntry(
    string TreeId,
    string ClassId,
    string BranchId,
    string NodeId,
    string Name,
    string Description,
    int MaxRank,
    IReadOnlyList<TalentAuditModifier> Modifiers);

public sealed record TalentAuditModifier(
    string CoverageId,
    TalentModifierType Type,
    string Key,
    string? TargetId,
    TalentModifierRuntimeStatus RuntimeStatus,
    string? DeferredOwner,
    IReadOnlyList<TalentAuditRank> Ranks);

public sealed record TalentAuditRank(int Rank, decimal Value, decimal? SecondaryValue);
