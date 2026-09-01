using Elyndor.Core.Content;

namespace Elyndor.Core.Talents;

/// <summary>
/// Validates talent allocation rules and calculates stat bonuses from talents.
/// Server-authoritative: never trusts client-provided talent states.
/// </summary>
public static class TalentCalculator
{
    /// <summary>
    /// Validates if a talent point can be allocated to a specific node.
    /// </summary>
    public static TalentAllocationResult CanAllocateTalent(
        CharacterTalents currentTalents,
        string talentId,
        TalentTreeProfile tree,
        int characterLevel)
    {
        // Find the talent node
        var node = FindTalentNode(tree, talentId);
        if (node == null)
            return new TalentAllocationResult(
                false,
                "TALENT_NOT_FOUND",
                $"Talent {talentId} does not exist in tree {tree.TalentTreeId}");

        // Check if already at max rank
        if (currentTalents.AllocatedPoints.TryGetValue(talentId, out int currentRank))
        {
            if (currentRank >= node.MaxRank)
                return new TalentAllocationResult(
                    false,
                    "TALENT_MAX_RANK",
                    $"Talent {node.Name} is already at max rank {node.MaxRank}");
        }

        // Check character level requirement (Tier * 5 + 1 <= level for Tier > 0)
        if (node.Tier > 0)
        {
            int minLevel = node.Tier * 5 + 1;
            if (characterLevel < minLevel)
                return new TalentAllocationResult(
                    false,
                    "LEVEL_REQUIREMENT_NOT_MET",
                    $"Talent {node.Name} requires level {minLevel} (Tier {node.Tier})");
        }

        // Check total available points based on level
        int availablePoints = Math.Max(0, characterLevel - 1); // Level 2-60: +1 per level
        if (currentTalents.TotalSpentPoints >= availablePoints)
            return new TalentAllocationResult(
                false,
                "NO_AVAILABLE_POINTS",
                $"No talent points available. Level {characterLevel} provides {availablePoints} points.");

        // Check branch spent requirement
        int branchSpent = CalculateBranchSpentPoints(currentTalents, tree, node.BranchId);
        if (branchSpent < node.RequiredSpentPointsInBranch)
            return new TalentAllocationResult(
                false,
                "BRANCH_REQUIREMENT_NOT_MET",
                $"Talent {node.Name} requires {node.RequiredSpentPointsInBranch} points in {node.BranchId} branch. Current: {branchSpent}");

        // Check prerequisites
        if (node.Prerequisites != null)
        {
            foreach (var prereqId in node.Prerequisites)
            {
                if (!currentTalents.AllocatedPoints.TryGetValue(prereqId, out int prereqRank) || prereqRank == 0)
                    return new TalentAllocationResult(
                        false,
                        "PREREQUISITE_NOT_MET",
                        $"Talent {node.Name} requires prerequisite talent {prereqId}");

                // Check if prerequisite is at max rank (for chain dependencies)
                var prereqNode = FindTalentNode(tree, prereqId);
                if (prereqNode != null && prereqRank < prereqNode.MaxRank)
                    return new TalentAllocationResult(
                        false,
                        "PREREQUISITE_NOT_MAXED",
                        $"Prerequisite talent {prereqId} must be at max rank");
            }
        }

        return new TalentAllocationResult(true, null, null);
    }

    /// <summary>
    /// Allocates a talent point to a specific node.
    /// Returns updated CharacterTalents if successful.
    /// </summary>
    public static TalentAllocationResult AllocateTalentPoint(
        CharacterTalents currentTalents,
        string talentId,
        TalentTreeProfile tree,
        int characterLevel,
        TimeProvider timeProvider)
    {
        var canAllocateResult = CanAllocateTalent(currentTalents, talentId, tree, characterLevel);
        if (!canAllocateResult.Success)
            return canAllocateResult;

        var node = FindTalentNode(tree, talentId)!;
        
        // Create new allocation dictionary with incremented rank
        var newAllocations = currentTalents.AllocatedPoints.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value);

        if (newAllocations.TryGetValue(talentId, out int currentRank))
            newAllocations[talentId] = currentRank + 1;
        else
            newAllocations[talentId] = 1;

        var updatedTalents = new CharacterTalents(
            currentTalents.CharacterId,
            currentTalents.TalentTreeId,
            newAllocations.AsReadOnly(),
            currentTalents.TotalSpentPoints + 1,
            currentTalents.TotalAvailablePoints,
            timeProvider.GetUtcNow());

        return new TalentAllocationResult(
            true,
            null,
            null,
            updatedTalents);
    }

    /// <summary>
    /// Calculates all stat modifiers from allocated talents.
    /// Returns PrimaryStats bonus to be added to base stats.
    /// </summary>
    public static PrimaryStats CalculateTalentStatBonuses(
        CharacterTalents talents,
        TalentTreeProfile tree)
    {
        decimal strength = 0m;
        decimal agility = 0m;
        decimal intellect = 0m;
        decimal stamina = 0m;

        foreach (var allocation in talents.AllocatedPoints)
        {
            string talentId = allocation.Key;
            int rank = allocation.Value;

            if (rank == 0) continue;

            var node = FindTalentNode(tree, talentId);
            if (node == null || node.StatValue == null || node.StatType == null)
                continue;

            // Calculate bonus based on rank (linear scaling for most talents)
            decimal bonusPerRank = node.StatValue.Value;
            decimal totalBonus = bonusPerRank * rank;

            // Apply to appropriate stat
            switch (node.StatType.ToUpperInvariant())
            {
                case "STRENGTH":
                case "STR":
                    strength += totalBonus;
                    break;
                case "AGILITY":
                case "AGI":
                    agility += totalBonus;
                    break;
                case "INTELLECT":
                case "INT":
                    intellect += totalBonus;
                    break;
                case "STAMINA":
                case "STA":
                    stamina += totalBonus;
                    break;
                // Percentage-based stats would be handled elsewhere (Armor%, Dodge%, etc.)
            }
        }

        return new PrimaryStats(strength, agility, intellect, stamina);
    }

    /// <summary>
    /// Gets all talents that grant new abilities.
    /// </summary>
    public static IReadOnlyList<string> GetGrantedAbilities(
        CharacterTalents talents,
        TalentTreeProfile tree)
    {
        var grantedAbilities = new List<string>();

        foreach (var allocation in talents.AllocatedPoints)
        {
            string talentId = allocation.Key;
            int rank = allocation.Value;

            if (rank == 0) continue;

            var node = FindTalentNode(tree, talentId);
            if (node != null && node.EffectType == "GrantAbility" && !string.IsNullOrEmpty(node.AbilityId))
            {
                grantedAbilities.Add(node.AbilityId);
            }
        }

        return grantedAbilities.AsReadOnly();
    }

    private static TalentNodeProfile? FindTalentNode(TalentTreeProfile tree, string talentId)
    {
        foreach (var branch in tree.Branches)
        {
            var node = branch.Nodes.FirstOrDefault(n => 
                string.Equals(n.TalentId, talentId, StringComparison.Ordinal));
            if (node != null)
                return node;
        }
        return null;
    }

    private static int CalculateBranchSpentPoints(CharacterTalents talents, TalentTreeProfile tree, string branchId)
    {
        int spent = 0;
        foreach (var allocation in talents.AllocatedPoints)
        {
            string talentId = allocation.Key;
            var node = FindTalentNode(tree, talentId);
            if (node != null && string.Equals(node.BranchId, branchId, StringComparison.Ordinal))
            {
                spent += allocation.Value;
            }
        }
        return spent;
    }
}
