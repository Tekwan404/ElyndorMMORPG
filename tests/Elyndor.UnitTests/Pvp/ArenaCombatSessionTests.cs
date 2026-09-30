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
    public void ReusedCommandIdIsRejectedWithExplicitError()
    {
        ArenaCombatSession session = Create();
        Assert.True(session.UseAbility(AccountA, "same", "STRIKE", ActorB, Start).Succeeded);
        decimal hp = session.Snapshot.ActorB.CurrentHp;
        ArenaCommandResult replay = session.UseAbility(AccountA, "same", "STRIKE", ActorB, Start.AddSeconds(1));
        Assert.False(replay.Succeeded);
        Assert.Equal("arena_duplicate_command", replay.ErrorCode);
        Assert.Equal(hp, session.Snapshot.ActorB.CurrentHp);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankCommandIdIsRejected(string commandId)
    {
        ArenaCombatSession session = Create();
        Assert.Equal("arena_invalid_command",
            session.UseAbility(AccountA, commandId, "STRIKE", ActorB, Start).ErrorCode);
    }

    [Fact]
    public void ConfiguredDurationDecidesTimeoutDraw()
    {
        ArenaCombatSession session = Create(duration: TimeSpan.FromSeconds(10));
        session.AdvanceTo(Start.AddSeconds(9));
        Assert.Equal(ArenaMatchOutcome.Active, session.Snapshot.Outcome);
        session.AdvanceTo(Start.AddSeconds(10));
        Assert.Equal(ArenaMatchOutcome.Draw, session.Snapshot.Outcome);
    }

    [Fact]
    public void CancelEndsWithoutWinnerOnlyOnce()
    {
        ArenaCombatSession session = Create();
        Assert.True(session.Cancel(Start.AddSeconds(1)));
        Assert.False(session.Cancel(Start.AddSeconds(2)));
        Assert.Equal(ArenaMatchOutcome.Cancelled, session.Snapshot.Outcome);
        Assert.False(session.UseAbility(AccountA, "late", "STRIKE", ActorB, Start.AddSeconds(3)).Succeeded);
        Assert.Single(session.GetEventsAfter(0), x => x.Type == CombatEventType.CombatEnded);
    }

    [Fact]
    public void ForfeitDoesNotOverrideAnEarlierDeath()
    {
        ArenaCombatSession session = Create(strikeDamage: 200, castTime: TimeSpan.FromSeconds(2));
        Assert.True(session.UseAbility(AccountA, "cast", "STRIKE", ActorB, Start).Succeeded);
        Assert.False(session.Forfeit(AccountA, Start.AddSeconds(3)));
        Assert.Equal(ArenaMatchOutcome.WinnerA, session.Snapshot.Outcome);
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

    [Fact]
    public void ForfeitFinishesTheMatchWithoutMoreGameplayOrDuplicateEnding()
    {
        ArenaCombatSession session = Create();
        Assert.True(session.Forfeit(AccountA, Start.AddSeconds(1)));
        Assert.False(session.Forfeit(AccountA, Start.AddSeconds(2)));
        Assert.Equal(ArenaMatchOutcome.WinnerB, session.Snapshot.Outcome);
        Assert.False(session.UseAbility(AccountB, "late", "STRIKE", ActorA,
            Start.AddSeconds(3)).Succeeded);
        Assert.Single(session.GetEventsAfter(0), x => x.Type == CombatEventType.CombatEnded);
    }

    [Fact]
    public void OffHandSwingUsesHalfIntervalAndStopsAfterCombatEnd()
    {
        ArenaCombatSession session = Create(firstOffHand: new AutoAttackProfile(
            TimeSpan.FromSeconds(2), 12, 0, 0));
        session.AdvanceTo(Start.AddMilliseconds(999));
        Assert.Equal(100, session.Snapshot.ActorB.CurrentHp);
        session.AdvanceTo(Start.AddSeconds(1));
        Assert.True(session.Snapshot.ActorB.CurrentHp < 100);
        Assert.True(session.Forfeit(AccountB, Start.AddSeconds(2)));
        decimal endedHp = session.Snapshot.ActorB.CurrentHp;
        session.AdvanceTo(Start.AddSeconds(5));
        Assert.Equal(endedHp, session.Snapshot.ActorB.CurrentHp);
    }

    [Fact]
    public void OffHandSwingDoesNotResolveDuringOwnCast()
    {
        ArenaCombatSession session = Create(castTime: TimeSpan.FromSeconds(3),
            firstOffHand: new AutoAttackProfile(TimeSpan.FromSeconds(2), 12, 0, 0));
        Assert.True(session.UseAbility(AccountA, "cast", "STRIKE", ActorB, Start).Succeeded);
        session.AdvanceTo(Start.AddSeconds(2));
        Assert.Equal(100, session.Snapshot.ActorB.CurrentHp);
    }

    [Fact]
    public void LethalOffHandAndMainHandAtSameTimestampResolveToDraw()
    {
        var noAbilities = new Dictionary<string, AbilityDefinition>();
        var first = new ArenaFighter(AccountA, ActorA,
            new CombatActorState(ActorA, 100, 100, 100, 100, CombatStats.Default),
            noAbilities, new AutoAttackProfile(TimeSpan.FromHours(1), 1, 0, 0),
            OffHandAutoAttack: new AutoAttackProfile(TimeSpan.FromSeconds(2), 200, 0, 0));
        var second = new ArenaFighter(AccountB, ActorB,
            new CombatActorState(ActorB, 100, 100, 100, 100, CombatStats.Default),
            noAbilities, new AutoAttackProfile(TimeSpan.FromSeconds(1), 200, 0, 0));
        var session = new ArenaCombatSession(Guid.NewGuid(), first, second,
            new SeededGameRandom(42), Start);

        session.AdvanceTo(Start.AddSeconds(1));

        Assert.Equal(ArenaMatchOutcome.Draw, session.Outcome);
        Assert.Single(session.GetEventsAfter(0), x => x.Type == CombatEventType.CombatEnded);
    }

    [Fact]
    public void SimultaneousLethalCastsResolveToDraw()
    {
        ArenaCombatSession session = Create(
            strikeDamage: 200,
            castTime: TimeSpan.FromSeconds(1));
        Assert.True(session.UseAbility(AccountA, "cast-a", "STRIKE", ActorB, Start).Succeeded);
        Assert.True(session.UseAbility(AccountB, "cast-b", "STRIKE", ActorA, Start).Succeeded);

        session.AdvanceTo(Start.AddSeconds(1));

        Assert.Equal(ArenaMatchOutcome.Draw, session.Snapshot.Outcome);
        Assert.Equal(0, session.Snapshot.ActorA.CurrentHp);
        Assert.Equal(0, session.Snapshot.ActorB.CurrentHp);
        Assert.Single(session.GetEventsAfter(0), combatEvent =>
            combatEvent.Type == CombatEventType.CombatEnded);
    }

    [Fact]
    public void SwappingFirstAndSecondDoesNotChangeSimultaneousLethalOutcome()
    {
        ArenaCombatSession normal = Create(
            strikeDamage: 200,
            castTime: TimeSpan.FromSeconds(1));
        ArenaCombatSession swapped = Create(
            strikeDamage: 200,
            castTime: TimeSpan.FromSeconds(1),
            swapFighters: true);

        Assert.True(normal.UseAbility(AccountA, "cast-a", "STRIKE", ActorB, Start).Succeeded);
        Assert.True(normal.UseAbility(AccountB, "cast-b", "STRIKE", ActorA, Start).Succeeded);
        Assert.True(swapped.UseAbility(AccountA, "cast-a", "STRIKE", ActorB, Start).Succeeded);
        Assert.True(swapped.UseAbility(AccountB, "cast-b", "STRIKE", ActorA, Start).Succeeded);

        normal.AdvanceTo(Start.AddSeconds(1));
        swapped.AdvanceTo(Start.AddSeconds(1));

        Assert.Equal(ArenaMatchOutcome.Draw, normal.Snapshot.Outcome);
        Assert.Equal(normal.Snapshot.Outcome, swapped.Snapshot.Outcome);
        Assert.Equal(0, swapped.Snapshot.ActorA.CurrentHp);
        Assert.Equal(0, swapped.Snapshot.ActorB.CurrentHp);
    }

    [Fact]
    public void SimultaneousLethalAutoAttacksResolveToDraw()
    {
        var lethalAuto = new AutoAttackProfile(
            TimeSpan.FromSeconds(1),
            200,
            0,
            0);
        ArenaCombatSession session = Create(autoAttack: lethalAuto);

        session.AdvanceTo(Start.AddSeconds(1));

        Assert.Equal(ArenaMatchOutcome.Draw, session.Snapshot.Outcome);
        Assert.Equal(0, session.Snapshot.ActorA.CurrentHp);
        Assert.Equal(0, session.Snapshot.ActorB.CurrentHp);
    }

    [Fact]
    public void SimultaneousLethalPendingActionsResolveToDraw()
    {
        var delayedLethal = new AbilityDefinition(
            "DELAYED_LETHAL",
            AbilityType.Instant,
            AbilityTargetType.SingleEnemy,
            0,
            TimeSpan.Zero,
            TimeSpan.Zero,
            false,
            GlobalCooldownCategory.None,
            false,
            "Physical",
            Actions:
            [
                new AbilityActionDefinition(
                    AbilityActionType.Damage,
                    200,
                    DamageType.Physical,
                    CanMiss: false,
                    CanCrit: false,
                    CanDodge: false,
                    Delay: TimeSpan.FromSeconds(1))
            ]);
        ArenaCombatSession session = Create(extraAbility: delayedLethal);
        Assert.True(session.UseAbility(
            AccountA, "delay-a", delayedLethal.Id, ActorB, Start).Succeeded);
        Assert.True(session.UseAbility(
            AccountB, "delay-b", delayedLethal.Id, ActorA, Start).Succeeded);

        session.AdvanceTo(Start.AddSeconds(1));

        Assert.Equal(ArenaMatchOutcome.Draw, session.Snapshot.Outcome);
        Assert.Equal(0, session.Snapshot.ActorA.CurrentHp);
        Assert.Equal(0, session.Snapshot.ActorB.CurrentHp);
    }

    [Fact]
    public void SimultaneousLethalPeriodicDamageResolvesToDraw()
    {
        ArenaCombatSession session = Create(dotDamage: 200);
        Assert.True(session.UseAbility(AccountA, "dot-a", "DOT", ActorB, Start).Succeeded);
        Assert.True(session.UseAbility(AccountB, "dot-b", "DOT", ActorA, Start).Succeeded);

        session.AdvanceTo(Start.AddSeconds(1));

        Assert.Equal(ArenaMatchOutcome.Draw, session.Snapshot.Outcome);
        Assert.Equal(0, session.Snapshot.ActorA.CurrentHp);
        Assert.Equal(0, session.Snapshot.ActorB.CurrentHp);
    }

    [Fact]
    public void ActionAfterLethalTimestampIsSuppressed()
    {
        ArenaCombatSession session = Create(
            strikeDamage: 200,
            castTime: TimeSpan.FromSeconds(1));
        Assert.True(session.UseAbility(AccountA, "cast-a", "STRIKE", ActorB, Start).Succeeded);
        Assert.True(session.UseAbility(
            AccountB,
            "cast-b",
            "STRIKE",
            ActorA,
            Start.AddMilliseconds(1)).Succeeded);

        session.AdvanceTo(Start.AddSeconds(1).AddMilliseconds(1));

        Assert.Equal(ArenaMatchOutcome.WinnerA, session.Snapshot.Outcome);
        Assert.Equal(100, session.Snapshot.ActorA.CurrentHp);
        Assert.DoesNotContain(session.GetEventsAfter(0), combatEvent =>
            combatEvent.Type == CombatEventType.AbilityCompleted
            && combatEvent.ActorId == ActorB);
    }

    [Fact]
    public void RepeatedAdvanceDoesNotReplayBatchAndCombatEndedIsEmittedOnce()
    {
        ArenaCombatSession session = Create(
            strikeDamage: 200,
            castTime: TimeSpan.FromSeconds(1));
        Assert.True(session.UseAbility(AccountA, "cast-a", "STRIKE", ActorB, Start).Succeeded);
        Assert.True(session.UseAbility(AccountB, "cast-b", "STRIKE", ActorA, Start).Succeeded);
        DateTimeOffset due = Start.AddSeconds(1);

        session.AdvanceTo(due);
        long sequenceAfterFirstAdvance = session.Snapshot.Sequence;
        session.AdvanceTo(due);

        Assert.Equal(sequenceAfterFirstAdvance, session.Snapshot.Sequence);
        Assert.Single(session.GetEventsAfter(0), combatEvent =>
            combatEvent.Type == CombatEventType.CombatEnded);
    }

    [Fact]
    public void TimestampBatchEventSequenceIsDeterministic()
    {
        ArenaCombatSession first = Create(
            strikeDamage: 200,
            castTime: TimeSpan.FromSeconds(1));
        ArenaCombatSession second = Create(
            strikeDamage: 200,
            castTime: TimeSpan.FromSeconds(1));
        Assert.True(first.UseAbility(AccountA, "cast-a", "STRIKE", ActorB, Start).Succeeded);
        Assert.True(first.UseAbility(AccountB, "cast-b", "STRIKE", ActorA, Start).Succeeded);
        Assert.True(second.UseAbility(AccountA, "cast-a", "STRIKE", ActorB, Start).Succeeded);
        Assert.True(second.UseAbility(AccountB, "cast-b", "STRIKE", ActorA, Start).Succeeded);

        first.AdvanceTo(Start.AddSeconds(1));
        second.AdvanceTo(Start.AddSeconds(1));

        var firstEvents = first.GetEventsAfter(0).Select(ProjectEvent).ToArray();
        var secondEvents = second.GetEventsAfter(0).Select(ProjectEvent).ToArray();
        Assert.Equal(firstEvents, secondEvents);
    }

    private static object ProjectEvent(CombatEvent combatEvent) => new
    {
        combatEvent.Type,
        combatEvent.OccurredAtUtc,
        combatEvent.ActorId,
        combatEvent.DefinitionId,
        combatEvent.Amount,
        combatEvent.SourceActorId,
        combatEvent.TargetActorId,
        combatEvent.IsPeriodic,
        combatEvent.DamageType
    };

    private static ArenaCombatSession Create(
        decimal strikeDamage = 20,
        TimeSpan? castTime = null,
        bool withShield = false,
        AbilityDefinition? extraAbility = null,
        TimeSpan? duration = null,
        decimal dotDamage = 20,
        AutoAttackProfile? autoAttack = null,
        bool swapFighters = false,
        AutoAttackProfile? firstOffHand = null)
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
                EffectStackPolicy.Replace, dotDamage, TimeSpan.FromSeconds(1),
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
        AutoAttackProfile auto = autoAttack ?? new AutoAttackProfile(TimeSpan.FromHours(1), 1, 0, 0);
        CombatStats stats = CombatStats.Default with { Accuracy = 100 };
        var actorA = new CombatActorState(ActorA, 100, 100, 100, 100, stats);
        var actorB = new CombatActorState(ActorB, 100, 100, 100, 100, stats);
        if (withShield) EffectEngine.Apply(actorB, ActorB, new EffectDefinition("SHIELD",
            EffectKind.Shield, TimeSpan.FromSeconds(10), 1, EffectStackPolicy.Replace, 30), Start);
        var fighterA = new ArenaFighter(AccountA, ActorA, actorA, abilities, auto,
            OffHandAutoAttack: firstOffHand);
        var fighterB = new ArenaFighter(AccountB, ActorB, actorB, abilities, auto);
        return new ArenaCombatSession(
            Guid.NewGuid(),
            swapFighters ? fighterB : fighterA,
            swapFighters ? fighterA : fighterB,
            new SeededGameRandom(42),
            Start,
            duration);
    }
}
