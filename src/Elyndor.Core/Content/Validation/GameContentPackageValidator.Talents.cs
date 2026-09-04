using Elyndor.Core.World;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Talents;
using Elyndor.Core.Monsters;
using Elyndor.Core.Items;

namespace Elyndor.Core.Content;

public static partial class GameContentPackageValidator
{
        internal static void ValidateTalentDefinitions(
            IReadOnlyList<TalentTreeDefinition> trees,
            IReadOnlyList<AbilityDefinition> abilities,
            List<ContentValidationError> errors)
        {
            HashSet<string> treeIds = [];
            for (var treeIndex = 0; treeIndex < trees.Count; treeIndex++)
            {
                TalentTreeDefinition tree = trees[treeIndex];
                string path = $"talentTrees[{treeIndex}]";
                if (!treeIds.Add(tree.Id))
                {
                    errors.Add(new("DUPLICATE_TALENT_TREE_ID", path, $"Talent tree '{tree.Id}' is duplicated."));
                }

                if (string.IsNullOrWhiteSpace(tree.Id) || string.IsNullOrWhiteSpace(tree.ClassId)
                    || tree.Version <= 0 || tree.MaxSpendablePoints <= 0)
                {
                    errors.Add(new("INVALID_TALENT_TREE", path, $"Talent tree '{tree.Id}' is invalid."));
                }

                HashSet<string> branchIds = [];
                foreach (TalentBranchDefinition branch in tree.Branches)
                {
                    if (!branchIds.Add(branch.Id) || branch.NodeCount <= 0)
                    {
                        errors.Add(new("INVALID_TALENT_BRANCH", path, $"Talent branch '{branch.Id}' is invalid or duplicated."));
                    }
                }

                Dictionary<string, TalentDefinition> nodes = [];
                HashSet<string> abilityIds = abilities.Select(ability => ability.Id).ToHashSet(StringComparer.Ordinal);
                foreach (TalentDefinition node in tree.Nodes)
                {
                    if (string.IsNullOrWhiteSpace(node.Id) || !nodes.TryAdd(node.Id, node))
                    {
                        errors.Add(new("DUPLICATE_TALENT_ID", path, $"Talent node '{node.Id}' is invalid or duplicated."));
                    }

                    if (!branchIds.Contains(node.BranchId) || node.Tier is < 1 or > 9
                        || node.RequiredSpentPoints < 0 || node.MaxRank <= 0 || node.Version <= 0
                        || node.RequiredLevel is < 1)
                    {
                        errors.Add(new("INVALID_TALENT_DEFINITION", path, $"Talent node '{node.Id}' is invalid."));
                    }

                    if (node.Modifiers is null || node.Modifiers.Count == 0)
                    {
                        errors.Add(new("MISSING_TALENT_MODIFIER", path,
                            $"Talent node '{node.Id}' must define a supported modifier or an explicit deferred hook."));
                    }
                    else if (node.Modifiers.Any(modifier => string.IsNullOrWhiteSpace(modifier.Key)
                        || !TalentModifierKeys.All.Contains(modifier.Key)
                        || modifier.Values.Count == 0 || modifier.Values.Any(value => value < 0)
                        || modifier.Values.Count != node.MaxRank
                        || modifier.SecondaryValues is { } secondary
                            && (secondary.Count != node.MaxRank || secondary.Any(value => value < 0))
                        || modifier.InternalCooldownSeconds < 0
                        || modifier.Threshold is < 0 or > 100
                        || modifier.ChancePercent is < 0 or > 100
                        || modifier.DurationSeconds < 0
                        || modifier.TickIntervalSeconds < 0
                        || modifier.TickIntervalSeconds > 0
                            && (modifier.DurationSeconds <= 0
                                || modifier.TickIntervalSeconds > modifier.DurationSeconds)
                        || modifier.TriggerCount < 0
                        || modifier.CastTimeSeconds < 0
                        || modifier.ResourceCostReductionPercent is < 0 or > 100
                        || modifier.RuntimeStatus == TalentModifierRuntimeStatus.Deferred
                            && (string.IsNullOrWhiteSpace(modifier.DeferredOwner)
                                || !TalentRuntimeOwners.All.Contains(modifier.DeferredOwner))))
                    {
                        errors.Add(new("INVALID_TALENT_MODIFIER", path, $"Talent node '{node.Id}' contains an invalid modifier."));
                    }

                    if (node.Modifiers?.Any(modifier =>
                        modifier.Type == TalentModifierType.AbilityModifier
                        && modifier.Key == TalentModifierKeys.UnlockAbility
                        && modifier.RuntimeStatus == TalentModifierRuntimeStatus.Supported
                        && (string.IsNullOrWhiteSpace(modifier.TargetId)
                            || !abilityIds.Contains(modifier.TargetId))) == true)
                    {
                        errors.Add(new("MISSING_TALENT_ABILITY_REFERENCE", path,
                            $"Talent node '{node.Id}' references an ability that does not exist."));
                    }

                    if (node.IconId is not null && !IsCanonicalIdentifier(node.IconId))
                    {
                        errors.Add(new("INVALID_TALENT_ICON_ID", path,
                            $"Talent node '{node.Id}' has a non-canonical icon id."));
                    }
                }

                foreach (TalentDefinition node in tree.Nodes)
                {
                    foreach (TalentPrerequisite prerequisite in node.Prerequisites)
                    {
                        if (!nodes.TryGetValue(prerequisite.TalentId, out TalentDefinition? required)
                            || prerequisite.RequiredRank <= 0
                            || prerequisite.RequiredRank > required.MaxRank)
                        {
                            errors.Add(new("INVALID_TALENT_PREREQUISITE", path,
                                $"Talent node '{node.Id}' has invalid prerequisite '{prerequisite.TalentId}'."));
                        }
                    }
                }

                foreach (TalentBranchDefinition branch in tree.Branches)
                {
                    if (tree.Nodes.Count(node => node.BranchId == branch.Id) != branch.NodeCount)
                    {
                        errors.Add(new("TALENT_BRANCH_NODE_COUNT_MISMATCH", path,
                            $"Talent branch '{branch.Id}' node count does not match its content."));
                    }
                }

                if (string.Equals(tree.Id, "WARRIOR_TREE", StringComparison.Ordinal)
                    && (tree.Nodes.Count != 96 || tree.Branches.Count != 3))
                {
                    errors.Add(new("INVALID_WARRIOR_TREE_SIZE", path,
                        "Warrior tree must contain exactly 96 nodes across 3 branches."));
                }

                if (HasTalentCycle(nodes))
                {
                    errors.Add(new("CIRCULAR_TALENT_PREREQUISITE", path,
                        $"Talent tree '{tree.Id}' contains a prerequisite cycle."));
                }
            }
        }

        private static bool HasTalentCycle(IReadOnlyDictionary<string, TalentDefinition> nodes)
        {
            HashSet<string> visiting = [];
            HashSet<string> visited = [];

            bool Visit(string id)
            {
                if (visited.Contains(id)) return false;
                if (!visiting.Add(id)) return true;
                if (nodes.TryGetValue(id, out TalentDefinition? node))
                {
                    foreach (TalentPrerequisite prerequisite in node.Prerequisites)
                    {
                        if (nodes.ContainsKey(prerequisite.TalentId) && Visit(prerequisite.TalentId))
                        {
                            return true;
                        }
                    }
                }

                visiting.Remove(id);
                visited.Add(id);
                return false;
            }

            return nodes.Keys.Any(Visit);
        }

}
