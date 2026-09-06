using Elyndor.Core.Items;

namespace Elyndor.Core.Talents;

public static class TalentEquipmentPermissionResolver
{
    public static bool HasPermission(
        TalentTreeDefinition tree,
        CharacterTalentState state,
        string permissionId)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionId);

        return HasPermission(
            tree,
            state.GetRanks(state.ActiveLoadoutId),
            permissionId);
    }

    public static bool HasPermission(
        TalentTreeDefinition tree,
        IReadOnlyDictionary<string, int> ranks,
        string permissionId)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(ranks);
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionId);

        if (!EquipmentPermissionIds.All.Contains(permissionId)) return false;

        foreach (TalentDefinition node in tree.Nodes)
        {
            if (!ranks.TryGetValue(node.Id, out int rank) || rank <= 0) continue;

            foreach (TalentModifierDefinition modifier in node.Modifiers ?? [])
            {
                if (modifier.Type != TalentModifierType.EquipmentConditional
                    || modifier.RuntimeStatus != TalentModifierRuntimeStatus.Supported
                    || modifier.Key != TalentModifierKeys.EquipmentConditional
                    || !string.Equals(modifier.TargetId, permissionId, StringComparison.Ordinal)
                    || modifier.Values.Count < rank
                    || modifier.Values[rank - 1] <= 0)
                {
                    continue;
                }

                return true;
            }
        }

        return false;
    }
}
