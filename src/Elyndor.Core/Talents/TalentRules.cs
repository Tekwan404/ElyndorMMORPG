namespace Elyndor.Core.Talents;

public static class TalentRules
{
    public static TalentLearnResult TryLearn(
        TalentTreeDefinition tree,
        int characterLevel,
        IReadOnlyDictionary<string, int> selectedRanks,
        string talentId)
    {
        Dictionary<string, int> unchanged = Copy(selectedRanks);
        TalentDefinition? talent = tree.Nodes.SingleOrDefault(node => node.Id == talentId);
        int earnedPoints = EarnedPoints(characterLevel, tree.MaxSpendablePoints);
        int spentPoints = selectedRanks.Values.Sum();
        int availablePoints = Math.Max(0, earnedPoints - spentPoints);

        if (talent is null)
        {
            return TalentLearnResult.Failure(TalentErrorCodes.UnknownTalent, unchanged, availablePoints);
        }

        int currentRank = selectedRanks.GetValueOrDefault(talent.Id);
        if (currentRank >= talent.MaxRank)
        {
            return TalentLearnResult.Failure(TalentErrorCodes.MaxRank, unchanged, availablePoints);
        }

        if (availablePoints < 1)
        {
            return TalentLearnResult.Failure(TalentErrorCodes.InsufficientPoints, unchanged, availablePoints);
        }

        if (talent.RequiredLevel is int requiredLevel && characterLevel < requiredLevel)
        {
            return TalentLearnResult.Failure(TalentErrorCodes.LevelRequired, unchanged, availablePoints);
        }

        int branchSpent = tree.Nodes
            .Where(node => node.BranchId == talent.BranchId)
            .Sum(node => selectedRanks.GetValueOrDefault(node.Id));
        if (branchSpent < talent.RequiredSpentPoints)
        {
            return TalentLearnResult.Failure(TalentErrorCodes.TierLocked, unchanged, availablePoints);
        }

        if (talent.Prerequisites.Any(requirement =>
            selectedRanks.GetValueOrDefault(requirement.TalentId) < requirement.RequiredRank))
        {
            return TalentLearnResult.Failure(
                TalentErrorCodes.PrerequisiteMissing,
                unchanged,
                availablePoints);
        }

        Dictionary<string, int> updated = Copy(selectedRanks);
        updated[talent.Id] = currentRank + 1;
        return new TalentLearnResult(true, null, updated, availablePoints - 1);
    }

    public static IReadOnlyList<string> ValidateBuild(
        TalentTreeDefinition tree,
        int characterLevel,
        IReadOnlyDictionary<string, int> selectedRanks)
    {
        List<string> errors = [];
        Dictionary<string, TalentDefinition> nodes = tree.Nodes.ToDictionary(node => node.Id);
        foreach ((string talentId, int rank) in selectedRanks)
        {
            if (!nodes.TryGetValue(talentId, out TalentDefinition? talent))
            {
                errors.Add(TalentErrorCodes.UnknownTalent);
                continue;
            }

            if (rank is < 1 || rank > talent.MaxRank)
            {
                errors.Add(TalentErrorCodes.InvalidRank);
            }
        }

        if (selectedRanks.Values.Where(rank => rank > 0).Sum()
            > EarnedPoints(characterLevel, tree.MaxSpendablePoints))
        {
            errors.Add(TalentErrorCodes.InsufficientPoints);
        }

        return errors.Distinct(StringComparer.Ordinal).ToArray();
    }

    public static int EarnedPoints(int characterLevel, int maxSpendablePoints) =>
        Math.Clamp(characterLevel - 1, 0, maxSpendablePoints);

    private static Dictionary<string, int> Copy(IReadOnlyDictionary<string, int> source) =>
        source.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
}
