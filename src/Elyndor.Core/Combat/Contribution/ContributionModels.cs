using Elyndor.Core.Combat.Participants;

namespace Elyndor.Core.Combat.Contribution;

public enum ParticipationEligibilityMode
{
    ContributionAndTime,
    ContributionOrQualifyingAction,
    TimeOrQualifyingAction
}

public sealed class ParticipationPolicy
{
    public ParticipationPolicy(
        TimeSpan minimumParticipationTime,
        int minimumQualifyingActions,
        decimal minimumContributionScore,
        decimal damageContributionWeight = 1,
        decimal healingContributionWeight = 1,
        decimal supportContributionWeight = 1,
        decimal tankingContributionWeight = 1,
        ParticipationEligibilityMode eligibilityMode = ParticipationEligibilityMode.ContributionAndTime)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(
            minimumParticipationTime,
            TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumQualifyingActions);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumContributionScore);
        ArgumentOutOfRangeException.ThrowIfNegative(damageContributionWeight);
        ArgumentOutOfRangeException.ThrowIfNegative(healingContributionWeight);
        ArgumentOutOfRangeException.ThrowIfNegative(supportContributionWeight);
        ArgumentOutOfRangeException.ThrowIfNegative(tankingContributionWeight);

        MinimumParticipationTime = minimumParticipationTime;
        MinimumQualifyingActions = minimumQualifyingActions;
        MinimumContributionScore = minimumContributionScore;
        DamageContributionWeight = damageContributionWeight;
        HealingContributionWeight = healingContributionWeight;
        SupportContributionWeight = supportContributionWeight;
        TankingContributionWeight = tankingContributionWeight;
        EligibilityMode = eligibilityMode;
    }

    public TimeSpan MinimumParticipationTime { get; }
    public int MinimumQualifyingActions { get; }
    public decimal MinimumContributionScore { get; }
    public decimal DamageContributionWeight { get; }
    public decimal HealingContributionWeight { get; }
    public decimal SupportContributionWeight { get; }
    public decimal TankingContributionWeight { get; }
    public ParticipationEligibilityMode EligibilityMode { get; }
}

public sealed record ContributionSnapshot(
    Guid CharacterId,
    DateTimeOffset JoinedAtUtc,
    DateTimeOffset? FledAtUtc,
    DateTimeOffset? DiedAtUtc,
    int QualifyingActions,
    decimal DamageDealt,
    decimal EffectiveHealing,
    decimal SupportContribution,
    decimal TankingContribution)
{
    public TimeSpan ParticipationDuration(DateTimeOffset completedAtUtc) =>
        (FledAtUtc ?? completedAtUtc) - JoinedAtUtc;
}

public sealed record ContributionEligibilityResult(
    bool IsEligible,
    string Reason,
    decimal ContributionScore,
    ContributionSnapshot Snapshot);

public sealed class ContributionLedger
{
    private readonly Dictionary<Guid, Entry> _entries = [];
    private readonly Dictionary<Guid, Guid> _characterByActorId;

    public ContributionLedger(IReadOnlyDictionary<Guid, Guid> characterByActorId)
    {
        ArgumentNullException.ThrowIfNull(characterByActorId);
        _characterByActorId = new Dictionary<Guid, Guid>(characterByActorId);
    }

    public void Register(Guid characterId, Guid actorId, DateTimeOffset joinedAtUtc)
    {
        if (_entries.ContainsKey(characterId))
            throw new InvalidOperationException($"Character '{characterId}' is already registered.");
        if (_characterByActorId.Any(item => item.Key == actorId && item.Value != characterId))
            throw new InvalidOperationException($"Actor '{actorId}' is already owned by another character.");

        _entries[characterId] = new Entry(characterId, actorId, joinedAtUtc);
    }

    public void Record(CombatEvent combatEvent)
    {
        ArgumentNullException.ThrowIfNull(combatEvent);
        Guid? actorId = combatEvent.Type is CombatEventType.ShieldAbsorbed
            or CombatEventType.DamageBlocked
            ? combatEvent.TargetActorId ?? combatEvent.ActorId
            : combatEvent.SourceActorId ??
              (combatEvent.Type is CombatEventType.HealingApplied
                  or CombatEventType.TauntApplied
                  or CombatEventType.EffectApplied
                  or CombatEventType.EffectRefreshed
                  or CombatEventType.AbilityUsed
                  or CombatEventType.ConsumableUsed
                  or CombatEventType.AutoAttackStarted
                  ? combatEvent.ActorId
                  : null);
        if (actorId is null
            || !_characterByActorId.TryGetValue(actorId.Value, out Guid characterId)
            || !_entries.TryGetValue(characterId, out Entry? entry)
            || entry.FledAtUtc is not null)
            return;

        switch (combatEvent.Type)
        {
            case CombatEventType.DamageDealt when combatEvent.Amount > 0:
                entry.DamageDealt += combatEvent.Amount;
                entry.QualifyingActions++;
                break;
            case CombatEventType.HealingApplied when combatEvent.Amount > 0:
                entry.EffectiveHealing += combatEvent.Amount;
                entry.QualifyingActions++;
                break;
            case CombatEventType.ShieldAbsorbed when combatEvent.Amount > 0:
                entry.SupportContribution += combatEvent.Amount;
                entry.QualifyingActions++;
                break;
            case CombatEventType.DamageBlocked when combatEvent.Amount > 0:
                entry.TankingContribution += combatEvent.Amount;
                entry.QualifyingActions++;
                break;
            case CombatEventType.TauntApplied:
                entry.TankingContribution += Math.Max(1, combatEvent.Amount);
                entry.QualifyingActions++;
                break;
            case CombatEventType.EffectApplied
                or CombatEventType.EffectRefreshed
                or CombatEventType.AbilityUsed
                or CombatEventType.ConsumableUsed
                or CombatEventType.AutoAttackStarted:
                entry.SupportContribution++;
                entry.QualifyingActions++;
                break;
        }
    }

    public void MarkFled(Guid characterId, DateTimeOffset fledAtUtc)
    {
        if (_entries.TryGetValue(characterId, out Entry? entry))
            entry.FledAtUtc = fledAtUtc;
    }

    public void MarkDied(Guid characterId, DateTimeOffset diedAtUtc)
    {
        if (_entries.TryGetValue(characterId, out Entry? entry))
            entry.DiedAtUtc = diedAtUtc;
    }

    public ContributionSnapshot GetSnapshot(Guid characterId)
    {
        if (!_entries.TryGetValue(characterId, out Entry? entry))
            throw new KeyNotFoundException($"Character '{characterId}' is not registered.");

        return entry.ToSnapshot();
    }

    public bool TryGetSnapshot(Guid characterId, out ContributionSnapshot? snapshot)
    {
        if (_entries.TryGetValue(characterId, out Entry? entry))
        {
            snapshot = entry.ToSnapshot();
            return true;
        }

        snapshot = null;
        return false;
    }

    public ContributionEligibilityResult Evaluate(
        Guid characterId,
        DateTimeOffset completedAtUtc,
        ParticipationPolicy policy)
    {
        if (!_entries.TryGetValue(characterId, out Entry? entry))
            throw new KeyNotFoundException($"Character '{characterId}' is not registered.");

        ContributionSnapshot snapshot = entry.ToSnapshot();
        TimeSpan participationTime = snapshot.ParticipationDuration(completedAtUtc);
        decimal score = snapshot.DamageDealt * policy.DamageContributionWeight
            + snapshot.EffectiveHealing * policy.HealingContributionWeight
            + snapshot.SupportContribution * policy.SupportContributionWeight
            + snapshot.TankingContribution * policy.TankingContributionWeight;
        bool hasTime = participationTime >= policy.MinimumParticipationTime;
        bool hasActions = snapshot.QualifyingActions >= policy.MinimumQualifyingActions;
        bool hasContribution = score >= policy.MinimumContributionScore;
        bool eligible = snapshot.FledAtUtc is null
            && policy.EligibilityMode switch
            {
                ParticipationEligibilityMode.ContributionAndTime => hasTime && hasContribution,
                ParticipationEligibilityMode.ContributionOrQualifyingAction =>
                    hasTime && (hasContribution || hasActions),
                ParticipationEligibilityMode.TimeOrQualifyingAction => hasTime || hasActions || hasContribution,
                _ => false
            };
        string reason = snapshot.FledAtUtc is not null
            ? "participant_fled"
            : eligible
                ? "eligible"
                : "insufficient_contribution";
        return new ContributionEligibilityResult(eligible, reason, score, snapshot);
    }

    private sealed class Entry(Guid characterId, Guid actorId, DateTimeOffset joinedAtUtc)
    {
        public Guid CharacterId { get; } = characterId;
        public Guid ActorId { get; } = actorId;
        public DateTimeOffset JoinedAtUtc { get; } = joinedAtUtc;
        public DateTimeOffset? FledAtUtc { get; set; }
        public DateTimeOffset? DiedAtUtc { get; set; }
        public int QualifyingActions { get; set; }
        public decimal DamageDealt { get; set; }
        public decimal EffectiveHealing { get; set; }
        public decimal SupportContribution { get; set; }
        public decimal TankingContribution { get; set; }

        public ContributionSnapshot ToSnapshot() => new(
            CharacterId,
            JoinedAtUtc,
            FledAtUtc,
            DiedAtUtc,
            QualifyingActions,
            DamageDealt,
            EffectiveHealing,
            SupportContribution,
            TankingContribution);
    }
}
