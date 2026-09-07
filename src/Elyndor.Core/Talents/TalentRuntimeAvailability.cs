namespace Elyndor.Core.Talents;

public static class TalentRuntimeAvailability
{
    public static bool IsModifierSupported(
        TalentDefinition node,
        TalentModifierDefinition modifier) =>
        modifier.RuntimeStatus == TalentModifierRuntimeStatus.Supported
        || BerserkerTalentRuntimeCatalog.SupportsLegacyDeferred(node, modifier)
        || PyromancerTalentRuntimeCatalog.SupportsLegacyDeferred(node, modifier)
        || MageTalentRuntimeCatalog.SupportsLegacyDeferred(node, modifier)
        || ArcherTalentRuntimeCatalog.SupportsLegacyDeferred(node, modifier);

    public static bool IsNodeFullySupported(TalentDefinition node) =>
        (node.Modifiers ?? []).All(modifier =>
            IsModifierSupported(node, modifier));

    public static string RuntimeStatus(TalentDefinition node)
    {
        IReadOnlyList<TalentModifierDefinition> modifiers = node.Modifiers ?? [];
        bool supported = modifiers.Any(modifier =>
            IsModifierSupported(node, modifier));
        bool deferred = modifiers.Any(modifier =>
            !IsModifierSupported(node, modifier));

        return (supported, deferred) switch
        {
            (true, true) => "PARTIAL",
            (true, false) => "SUPPORTED",
            (false, true) => "DEFERRED",
            _ => "SUPPORTED"
        };
    }
}
