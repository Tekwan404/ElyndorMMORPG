using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Contribution;

namespace Elyndor.UnitTests.Combat;

public sealed class ContributionPolicyTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SupportOnlyHealingQualifiesWithoutDamage()
    {
        Guid actorId = Guid.NewGuid();
        Guid characterId = Guid.NewGuid();
        ContributionLedger ledger = CreateLedger(characterId, actorId);
        ledger.Record(new CombatEvent(
            CombatEventType.HealingApplied,
            Start.AddSeconds(30),
            actorId,
            Amount: 100,
            SourceActorId: actorId));

        ContributionEligibilityResult result = ledger.Evaluate(
            characterId,
            Start.AddSeconds(31),
            Policy());

        Assert.True(result.IsEligible);
        Assert.Equal(0, result.Snapshot.DamageDealt);
        Assert.Equal(100, result.Snapshot.EffectiveHealing);
    }

    [Fact]
    public void TankingAndMitigationQualifyWithoutDamage()
    {
        Guid actorId = Guid.NewGuid();
        Guid enemyActorId = Guid.NewGuid();
        Guid characterId = Guid.NewGuid();
        ContributionLedger ledger = CreateLedger(characterId, actorId);
        ledger.Record(new CombatEvent(
            CombatEventType.TauntApplied,
            Start.AddSeconds(5),
            enemyActorId,
            Amount: 4,
            SourceActorId: actorId,
            TargetActorId: enemyActorId));
        ledger.Record(new CombatEvent(
            CombatEventType.DamageBlocked,
            Start.AddSeconds(10),
            actorId,
            Amount: 50,
            SourceActorId: enemyActorId,
            TargetActorId: actorId));
        ledger.Record(new CombatEvent(
            CombatEventType.ShieldAbsorbed,
            Start.AddSeconds(11),
            actorId,
            Amount: 25,
            SourceActorId: enemyActorId,
            TargetActorId: actorId));

        ContributionEligibilityResult result = ledger.Evaluate(
            characterId,
            Start.AddSeconds(30),
            Policy());

        Assert.True(result.IsEligible);
        Assert.Equal(0, result.Snapshot.DamageDealt);
        Assert.Equal(54, result.Snapshot.TankingContribution);
        Assert.Equal(25, result.Snapshot.SupportContribution);
    }

    [Fact]
    public void ValidParticipantWhoDiesRemainsEligible()
    {
        Guid actorId = Guid.NewGuid();
        Guid characterId = Guid.NewGuid();
        ContributionLedger ledger = CreateLedger(characterId, actorId);
        ledger.Record(new CombatEvent(
            CombatEventType.DamageDealt,
            Start.AddSeconds(10),
            actorId,
            Amount: 20,
            SourceActorId: actorId));
        ledger.MarkDied(characterId, Start.AddSeconds(20));

        ContributionEligibilityResult result = ledger.Evaluate(
            characterId,
            Start.AddSeconds(30),
            Policy());

        Assert.True(result.IsEligible);
        Assert.Equal(Start.AddSeconds(20), result.Snapshot.DiedAtUtc);
    }

    [Fact]
    public void FledParticipantIsNotEligibleEvenAfterValidActions()
    {
        Guid actorId = Guid.NewGuid();
        Guid characterId = Guid.NewGuid();
        ContributionLedger ledger = CreateLedger(characterId, actorId);
        ledger.Record(new CombatEvent(
            CombatEventType.DamageDealt,
            Start.AddSeconds(10),
            actorId,
            Amount: 100,
            SourceActorId: actorId));
        ledger.MarkFled(characterId, Start.AddSeconds(20));

        ContributionEligibilityResult result = ledger.Evaluate(
            characterId,
            Start.AddSeconds(30),
            Policy());

        Assert.False(result.IsEligible);
        Assert.Equal("participant_fled", result.Reason);
    }

    [Fact]
    public void CompanionEventsAreAttributedToItsOwningCharacter()
    {
        Guid characterId = Guid.NewGuid();
        Guid playerActorId = Guid.NewGuid();
        Guid companionActorId = Guid.NewGuid();
        ContributionLedger ledger = new(new Dictionary<Guid, Guid>
        {
            [playerActorId] = characterId,
            [companionActorId] = characterId
        });
        ledger.Register(characterId, playerActorId, Start);
        ledger.Record(new CombatEvent(
            CombatEventType.DamageDealt,
            Start.AddSeconds(10),
            companionActorId,
            Amount: 25,
            SourceActorId: companionActorId));

        ContributionEligibilityResult result = ledger.Evaluate(
            characterId,
            Start.AddSeconds(20),
            Policy());

        Assert.True(result.IsEligible);
        Assert.Equal(25, result.Snapshot.DamageDealt);
    }

    private static ContributionLedger CreateLedger(Guid characterId, Guid actorId)
    {
        ContributionLedger ledger = new(new Dictionary<Guid, Guid>
        {
            [actorId] = characterId
        });
        ledger.Register(characterId, actorId, Start);
        return ledger;
    }

    private static ParticipationPolicy Policy() => new(
        minimumParticipationTime: TimeSpan.FromSeconds(10),
        minimumQualifyingActions: 1,
        minimumContributionScore: 1,
        eligibilityMode: ParticipationEligibilityMode.ContributionAndTime);
}
