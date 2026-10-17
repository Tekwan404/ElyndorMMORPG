namespace Elyndor.Core.Pvp;

/// <summary>
/// Server-captured identity and eligibility facts for a potential world PvP attack.
/// These values must come from authoritative character/location state, never a client request.
/// </summary>
public sealed record WorldPvpCombatant(
    Guid CharacterId,
    Guid AccountId,
    string RaceId,
    int Level,
    string LocationId,
    bool IsAlive = true,
    bool IsTravelling = false);

public enum WorldPvpAttackFailure
{
    None,
    InvalidParticipant,
    SameCharacter,
    SameAccount,
    DifferentLocation,
    ZoneUnavailable,
    LevelRequired,
    SameFaction,
    ParticipantUnavailable
}

/// <summary>
/// The admission policy for future open-world PvP combat, independent of Arena and PvE sessions.
/// This policy does not start combat or grant rewards.
/// </summary>
public static class WorldPvpRules
{
    public const int MinimumLevel = 60;
    public const string ContestedLocationId = "DEAD_REACHES";

    public static bool IsContestedLocation(string? locationId) =>
        string.Equals(locationId, ContestedLocationId, StringComparison.Ordinal);

    public static WorldPvpAttackFailure CheckAttack(
        WorldPvpCombatant attacker,
        WorldPvpCombatant target)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);

        if (attacker.CharacterId == Guid.Empty || target.CharacterId == Guid.Empty
            || attacker.AccountId == Guid.Empty || target.AccountId == Guid.Empty
            || !IsKnownFaction(attacker.RaceId) || !IsKnownFaction(target.RaceId)
            || string.IsNullOrWhiteSpace(attacker.LocationId)
            || string.IsNullOrWhiteSpace(target.LocationId))
        {
            return WorldPvpAttackFailure.InvalidParticipant;
        }

        if (attacker.CharacterId == target.CharacterId)
            return WorldPvpAttackFailure.SameCharacter;

        if (attacker.AccountId == target.AccountId)
            return WorldPvpAttackFailure.SameAccount;

        if (!string.Equals(attacker.LocationId, target.LocationId, StringComparison.Ordinal))
            return WorldPvpAttackFailure.DifferentLocation;

        if (!IsContestedLocation(attacker.LocationId))
            return WorldPvpAttackFailure.ZoneUnavailable;

        if (attacker.Level < MinimumLevel || target.Level < MinimumLevel)
            return WorldPvpAttackFailure.LevelRequired;

        if (attacker.RaceId == target.RaceId)
            return WorldPvpAttackFailure.SameFaction;

        if (!attacker.IsAlive || !target.IsAlive || attacker.IsTravelling || target.IsTravelling)
            return WorldPvpAttackFailure.ParticipantUnavailable;

        return WorldPvpAttackFailure.None;
    }

    private static bool IsKnownFaction(string raceId) =>
        raceId is "HUMAN" or "UNDEAD";
}
