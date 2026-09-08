namespace Elyndor.Core.Combat;

public sealed class CombatSharedLootResolution
{
    private CombatSharedLootResolution()
    {
        GroupLootJson = null!;
    }

    public CombatSharedLootResolution(
        Guid combatSessionId,
        string groupLootJson,
        DateTimeOffset resolvedAtUtc)
    {
        if (combatSessionId == Guid.Empty)
            throw new ArgumentException("Combat session identifier cannot be empty.", nameof(combatSessionId));
        ArgumentException.ThrowIfNullOrWhiteSpace(groupLootJson);
        if (resolvedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Shared loot timestamps must be UTC.", nameof(resolvedAtUtc));

        CombatSessionId = combatSessionId;
        GroupLootJson = groupLootJson;
        ResolvedAtUtc = resolvedAtUtc;
    }

    public Guid CombatSessionId { get; private set; }
    public string GroupLootJson { get; private set; }
    public DateTimeOffset ResolvedAtUtc { get; private set; }
}
