using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Pvp;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaCombatSessionTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid AccountA = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid AccountB = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid ActorA = Guid.Parse("30000000-0000-0000-0000-000000000003");
    private static readonly Guid ActorB = Guid.Parse("40000000-0000-0000-0000-000000000004");

    [Fact]
    public void OnlyOwnerCanCommandTheirActorAndOnlyOpponentIsHostileTarget()
    {
        ArenaCombatSession session = Create();
        Assert.False(session.UseAbility(Guid.NewGuid(), "unauthorized", "STRIKE", ActorB, Start).Succeeded);
        Assert.False(session.UseAbility(AccountA, "outside", "STRIKE", Guid.NewGuid(), Start).Succeeded);
        Assert.False(session.UseAbility(AccountA, "self", "STRIKE", ActorA, Start).Succeeded);
        Assert.True(session.UseAbility(AccountA, "valid", "STRIKE", ActorB, Start).Succeeded);
        Assert.True(session.Snapshot.ActorB.CurrentHp < 100);
    }

    [Fact]
    public void NonParticipantCommandCannotAdvanceOrEndMatch()
    {
        ArenaCombatSession session = Create();
        var result = session.UseAbility(Guid.NewGuid(), "intruder", "STRIKE", ActorB,
            Start.AddMinutes(6));
        Assert.False(result.Succeeded);
        Assert.Equal("arena_not_participant", result.ErrorCode);
        Assert.Equal(ArenaMatchOutcome.Active, session.Snapshot.Outcome);
    }

    [Fact]
    public void DuplicateCommandCannotDealDamageTwice()
    {
        ArenaCombatSession session = Create();
        Assert.True(session.UseAbility(AccountA, "one", "STRIKE", ActorB, Start).Succeeded);
        decimal remaining = session.Snapshot.ActorB.CurrentHp;
        Assert.False(session.UseAbility(AccountA, "one", "STRIKE", ActorB, Start.AddSeconds(2)).Succeeded);
        Assert.Equal(remaining, session.Snapshot.ActorB.CurrentHp);
    }

    [Fact]
    public void SelfOnlyHealCannotBeRedirectedToOpponent()
    {
        ArenaCombatSession session = Create();
        Assert.False(session.UseAbility(AccountA, "bad-heal", "SELF_HEAL", ActorB, Start).Succeeded);
    }

    [Fact]
    public void LethalHitEndsMatchAndRejectsMoreGameplayCommands()
    {
        ArenaCombatSession session = Create(strikeDamage: 200);
        Assert.True(session.UseAbility(AccountA, "kill", "STRIKE", ActorB, Start).Succeeded);
        Assert.Equal(ArenaMatchOutcome.WinnerA, session.Snapshot.Outcome);
        Assert.False(session.UseAbility(AccountA, "late", "STRIKE", ActorB, Start.AddSeconds(2)).Succeeded);
        Assert.Equal(0, session.Snapshot.ActorB.CurrentHp);
    }

    [Fact]
    public void CastResolvesOnlyWhenAuthoritativeTimeAdvances()
    {
        ArenaCombatSession session = Create(castTime: TimeSpan.FromSeconds(2));
        Assert.True(session.UseAbility(AccountA, "cast", "STRIKE", ActorB, Start).Succeeded);
        Assert.Equal(100, session.Snapshot.ActorB.CurrentHp);
        session.AdvanceTo(Start.AddSeconds(1));
        Assert.Equal(100, session.Snapshot.ActorB.CurrentHp);
        session.AdvanceTo(Start.AddSeconds(2));
        Assert.True(session.Snapshot.ActorB.CurrentHp < 100);
    }

    [Fact]
    public void MatchTimesOutEvenWhenNeitherActorHasAnEarlierScheduledAction()
    {
        ArenaCombatSession session = Create();
        session.AdvanceTo(Start.AddMinutes(6));
        Assert.Equal(ArenaMatchOutcome.Draw, session.Snapshot.Outcome);
    }

    [Fact]
    public void PeriodicDamageUsesTheSharedShieldAndMitigationPipeline()
    {
        ArenaCombatSession session = Create(withShield: true);
        Assert.True(session.UseAbility(AccountA, "dot", "DOT", ActorB, Start).Succeeded);
        session.AdvanceTo(Start.AddSeconds(1));
        Assert.Equal(100, session.Snapshot.ActorB.CurrentHp);
    }

    [Fact]
    public void InterruptCancelsAnOpponentsPendingCast()
    {
        ArenaCombatSession session = Create(castTime: TimeSpan.FromSeconds(2));
        Assert.True(session.UseAbility(AccountB, "cast", "STRIKE", ActorA, Start).Succeeded);
        Assert.True(session.UseAbility(AccountA, "kick", "KICK", ActorB, Start.AddMilliseconds(500)).Succeeded);
        session.AdvanceTo(Start.AddSeconds(2));
        Assert.Equal(100, session.Snapshot.ActorA.CurrentHp);
        Assert.Contains(session.GetEventsAfter(0), x => x.Type == CombatEventType.AbilityInterrupted);
    }

    [Fact]
    public void AreaEnemyAbilityHitsOnlyTheOpposingPlayer()
    {
        ArenaCombatSession session = Create();
        decimal before = session.Snapshot.ActorB.CurrentHp;
        var result = session.UseAbility(AccountA, "area", "AREA", ActorB, Start);
        Assert.True(result.Succeeded);
        Assert.True(session.Snapshot.ActorB.CurrentHp < before);
        Assert.Equal(100, session.Snapshot.ActorA.CurrentHp);
    }

    [Fact]
    public void BuildWithPveOnlyRuntimeParametersIsRejectedInsteadOfSilentlySpendingResource()
    {
        var unsupported = new AbilityDefinition("COMPANION_COMMAND", AbilityType.Instant,
            AbilityTargetType.SingleEnemy, 20, TimeSpan.Zero, TimeSpan.Zero,
            false, GlobalCooldownCategory.None, false, "Physical",
            RuntimeParameters: new Dictionary<string, decimal> { ["companionDamageMultiplier"] = 1.5m });
        Assert.Throws<NotSupportedException>(() => Create(extraAbility: unsupported));
    }

    [Fact]
    public void BuildWithOnExpireEffectIsRejectedUntilExpirationActionsAreSupported()
    {
        var effect = new EffectDefinition("EXPLOSIVE", EffectKind.Debuff, TimeSpan.FromSeconds(2),
            1, EffectStackPolicy.Replace, 0,
            OnExpireActions: [new EffectExpirationActionDefinition(EffectExpirationActionType.Damage, 30)]);
        var unsupported = new AbilityDefinition("BOMB", AbilityType.Instant,
            AbilityTargetType.SingleEnemy, 0, TimeSpan.Zero, TimeSpan.Zero,
            false, GlobalCooldownCategory.None, true, "Fire",
            Actions: [new AbilityActionDefinition(AbilityActionType.ApplyEffect, Effect: effect)]);
        Assert.Throws<NotSupportedException>(() => Create(extraAbility: unsupported));
    }

    private static ArenaCombatSession Create(decimal strikeDamage = 20, TimeSpan? castTime = null,
        bool withShield = false, AbilityDefinition? extraAbility = null)
    {
        var strike = new AbilityDefinition("STRIKE", castTime is null ? AbilityType.Instant : AbilityType.Casted,
            AbilityTargetType.SingleEnemy, 0, TimeSpan.Zero, castTime ?? TimeSpan.Zero,
            false, GlobalCooldownCategory.None, false, "Physical",
            Actions: [new AbilityActionDefinition(AbilityActionType.Damage, strikeDamage,
                DamageType.Physical, CanMiss: false, CanCrit: false, CanDodge: false)]);
        var heal = new AbilityDefinition("SELF_HEAL", AbilityType.Instant, AbilityTargetType.Self,
            0, TimeSpan.Zero, TimeSpan.Zero, false, GlobalCooldownCategory.None, false, "Holy",
            Actions: [new AbilityActionDefinition(AbilityActionType.Healing, 20)]);
        var dot = new AbilityDefinition("DOT", AbilityType.Instant, AbilityTargetType.SingleEnemy,
            0, TimeSpan.Zero, TimeSpan.Zero, false, GlobalCooldownCategory.None, true, "Shadow",
            Actions: [new AbilityActionDefinition(AbilityActionType.ApplyEffect, Effect: new EffectDefinition(
                "ARENA_DOT", EffectKind.DamageOverTime, TimeSpan.FromSeconds(3), 1,
                EffectStackPolicy.Replace, 20, TimeSpan.FromSeconds(1),
                PeriodicDamageType: DamageType.Magical))]);
        var kick = new AbilityDefinition("KICK", AbilityType.Instant, AbilityTargetType.SingleEnemy,
            0, TimeSpan.Zero, TimeSpan.Zero, false, GlobalCooldownCategory.None, false, "Physical",
            Actions: [new AbilityActionDefinition(AbilityActionType.Interrupt,
                InterruptLockout: TimeSpan.FromSeconds(2))]);
        var abilities = new Dictionary<string, AbilityDefinition>
        {
            [strike.Id] = strike, [heal.Id] = heal, [dot.Id] = dot, [kick.Id] = kick,
            ["AREA"] = new AbilityDefinition("AREA", AbilityType.Instant,
                AbilityTargetType.AllEnemiesInCombat, 0, TimeSpan.Zero, TimeSpan.Zero,
                false, GlobalCooldownCategory.None, false, "Fire",
                Actions: [new AbilityActionDefinition(AbilityActionType.Damage, 20,
                    DamageType.Magical, CanMiss: false, CanCrit: false, CanDodge: false)])
        };
        if (extraAbility is not null) abilities.Add(extraAbility.Id, extraAbility);
        var auto = new AutoAttackProfile(TimeSpan.FromHours(1), 1, 0, 0);
        var secondActor = new CombatActorState(ActorB, 100, 100, 100, 100, CombatStats.Default);
        if (withShield) EffectEngine.Apply(secondActor, ActorB, new EffectDefinition("SHIELD",
            EffectKind.Shield, TimeSpan.FromSeconds(10), 1, EffectStackPolicy.Replace, 30), Start);
        return new ArenaCombatSession(Guid.NewGuid(),
            new ArenaFighter(AccountA, ActorA, new CombatActorState(ActorA, 100, 100, 100, 100, CombatStats.Default), abilities, auto),
            new ArenaFighter(AccountB, ActorB, secondActor, abilities, auto),
            new SeededGameRandom(42), Start);
    }
}
