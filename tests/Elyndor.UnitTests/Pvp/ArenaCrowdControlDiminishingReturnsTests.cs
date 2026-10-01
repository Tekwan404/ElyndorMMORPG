using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Pvp;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaCrowdControlDiminishingReturnsTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 29, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid AccountA = Guid.Parse("51000000-0000-0000-0000-000000000001");
    private static readonly Guid AccountB = Guid.Parse("52000000-0000-0000-0000-000000000002");
    private static readonly Guid ActorA = Guid.Parse("53000000-0000-0000-0000-000000000003");
    private static readonly Guid ActorB = Guid.Parse("54000000-0000-0000-0000-000000000004");

    [Fact]
    public void SameCategoryScalesToHalfQuarterThenImmune()
    {
        CrowdControlDiminishingReturns dr = new();
        TimeSpan baseDuration = TimeSpan.FromSeconds(4);

        CrowdControlDrResolution first = dr.Resolve(CrowdControlCategory.Stun, baseDuration, Start);
        Assert.Equal(0, first.Level);
        Assert.False(first.IsImmune);
        Assert.Equal(baseDuration, first.EffectiveDuration);
        dr.Commit(first, Start + first.EffectiveDuration);

        DateTimeOffset secondAt = Start.AddSeconds(4);
        CrowdControlDrResolution second = dr.Resolve(CrowdControlCategory.Stun, baseDuration, secondAt);
        Assert.Equal(1, second.Level);
        Assert.Equal(TimeSpan.FromSeconds(2), second.EffectiveDuration);
        dr.Commit(second, secondAt + second.EffectiveDuration);

        DateTimeOffset thirdAt = Start.AddSeconds(6);
        CrowdControlDrResolution third = dr.Resolve(CrowdControlCategory.Stun, baseDuration, thirdAt);
        Assert.Equal(2, third.Level);
        Assert.Equal(TimeSpan.FromSeconds(1), third.EffectiveDuration);
        dr.Commit(third, thirdAt + third.EffectiveDuration);

        CrowdControlDrResolution fourth = dr.Resolve(
            CrowdControlCategory.Stun,
            baseDuration,
            Start.AddSeconds(7));
        Assert.Equal(3, fourth.Level);
        Assert.True(fourth.IsImmune);
        Assert.Equal(TimeSpan.Zero, fourth.EffectiveDuration);
    }

    [Fact]
    public void DrScalingNeverRoundsPositiveControlToZero()
    {
        CrowdControlDiminishingReturns dr = new();
        TimeSpan oneTick = TimeSpan.FromTicks(1);

        CrowdControlDrResolution first = dr.Resolve(CrowdControlCategory.Stun, oneTick, Start);
        dr.Commit(first, Start + first.EffectiveDuration);
        CrowdControlDrResolution second = dr.Resolve(
            CrowdControlCategory.Stun,
            oneTick,
            Start.AddTicks(1));
        dr.Commit(second, Start.AddTicks(1) + second.EffectiveDuration);
        CrowdControlDrResolution third = dr.Resolve(
            CrowdControlCategory.Stun,
            oneTick,
            Start.AddTicks(2));

        Assert.Equal(TimeSpan.FromTicks(1), second.EffectiveDuration);
        Assert.Equal(TimeSpan.FromTicks(1), third.EffectiveDuration);
    }

    [Fact]
    public void ResetWindowStartsWhenLastControlEnds()
    {
        CrowdControlDiminishingReturns dr = new();
        CrowdControlDrResolution first = dr.Resolve(
            CrowdControlCategory.Stun,
            TimeSpan.FromSeconds(4),
            Start);
        dr.Commit(first, Start.AddSeconds(4));

        Assert.Equal(1, dr.Resolve(
            CrowdControlCategory.Stun,
            TimeSpan.FromSeconds(4),
            Start.AddSeconds(18).AddMilliseconds(999)).Level);

        CrowdControlDrResolution reset = dr.Resolve(
            CrowdControlCategory.Stun,
            TimeSpan.FromSeconds(4),
            Start.AddSeconds(19));
        Assert.Equal(0, reset.Level);
        Assert.Equal(TimeSpan.FromSeconds(4), reset.EffectiveDuration);
    }

    [Fact]
    public void DifferentCategoriesHaveIndependentState()
    {
        CrowdControlDiminishingReturns dr = new();
        CrowdControlDrResolution stun = dr.Resolve(
            CrowdControlCategory.Stun,
            TimeSpan.FromSeconds(4),
            Start);
        dr.Commit(stun, Start.AddSeconds(4));

        CrowdControlDrResolution root = dr.Resolve(
            CrowdControlCategory.Root,
            TimeSpan.FromSeconds(4),
            Start.AddSeconds(4));

        Assert.Equal(0, root.Level);
        Assert.Equal(TimeSpan.FromSeconds(4), root.EffectiveDuration);
    }

    [Theory]
    [InlineData(EffectKind.Stun, CrowdControlCategory.Stun)]
    [InlineData(EffectKind.Root, CrowdControlCategory.Root)]
    [InlineData(EffectKind.Fear, CrowdControlCategory.Fear)]
    [InlineData(EffectKind.Silence, CrowdControlCategory.Silence)]
    [InlineData(EffectKind.Disarm, CrowdControlCategory.Disarm)]
    public void ExistingControlKindsMapToSharedArenaCategories(
        EffectKind effectKind,
        CrowdControlCategory expected)
    {
        Assert.True(CrowdControlCategoryResolver.TryResolve(effectKind, out CrowdControlCategory actual));
        Assert.Equal(expected, actual);
        Assert.Equal("Incapacitate", CrowdControlCategory.Incapacitate.ToString());
    }

    [Fact]
    public void ArenaAppliesFullHalfQuarterThenReportsImmune()
    {
        ArenaCombatSession session = CreateArena();

        Assert.True(Use(session, AccountA, "stun-1", "STUN_A", ActorB, 0).Succeeded);
        Assert.Equal("ActorStunned", Use(session, AccountB, "blocked-full", "STRIKE", ActorA, 3).ErrorCode);
        Assert.True(Use(session, AccountB, "after-full", "STRIKE", ActorA, 4).Succeeded);

        Assert.True(Use(session, AccountA, "stun-2", "STUN_A", ActorB, 4).Succeeded);
        Assert.Equal("ActorStunned", Use(session, AccountB, "blocked-half", "STRIKE", ActorA, 5).ErrorCode);
        Assert.True(Use(session, AccountB, "after-half", "STRIKE", ActorA, 6).Succeeded);

        Assert.True(Use(session, AccountA, "stun-3", "STUN_A", ActorB, 6).Succeeded);
        Assert.Equal("ActorStunned", UseAt(session, AccountB, "blocked-quarter", "STRIKE", ActorA,
            Start.AddSeconds(6.5)).ErrorCode);

        ArenaCommandResult immune = Use(session, AccountA, "stun-4", "STUN_A", ActorB, 7);
        Assert.True(immune.Succeeded);
        CombatEvent immunity = Assert.Single(immune.Events, item => item.Type == CombatEventType.EffectImmune);
        Assert.Equal("STUN_A_EFFECT", immunity.DefinitionId);
        Assert.True(Use(session, AccountB, "acts-while-immune", "STRIKE", ActorA, 7).Succeeded);
    }

    [Fact]
    public void DifferentStunAbilitiesShareOneDrCounter()
    {
        ArenaCombatSession session = CreateArena();
        Assert.True(Use(session, AccountA, "a", "STUN_A", ActorB, 0).Succeeded);
        Assert.True(Use(session, AccountA, "b", "STUN_B", ActorB, 4).Succeeded);

        Assert.Equal("ActorStunned", UseAt(session, AccountB, "still-half", "STRIKE", ActorA,
            Start.AddSeconds(5.5)).ErrorCode);
        Assert.True(Use(session, AccountB, "half-ended", "STRIKE", ActorA, 6).Succeeded);
    }

    [Fact]
    public void RootKeepsFullDurationWhenStunAlreadyHasDr()
    {
        ArenaCombatSession session = CreateArena();
        Assert.True(Use(session, AccountA, "stun", "STUN_A", ActorB, 0).Succeeded);
        Assert.True(Use(session, AccountA, "root", "ROOT", ActorB, 4).Succeeded);

        Assert.Equal("ActorRooted", Use(session, AccountB, "rooted-dash", "DASH", ActorA, 7).ErrorCode);
        Assert.True(Use(session, AccountB, "root-ended", "DASH", ActorA, 8).Succeeded);
    }

    [Fact]
    public void ArenaDrResetsFifteenSecondsAfterControlEnds()
    {
        ArenaCombatSession session = CreateArena();
        Assert.True(Use(session, AccountA, "first", "STUN_A", ActorB, 0).Succeeded);

        Assert.True(Use(session, AccountA, "after-reset", "STUN_A", ActorB, 19).Succeeded);
        Assert.Equal("ActorStunned", UseAt(session, AccountB, "full-again", "STRIKE", ActorA,
            Start.AddSeconds(22.5)).ErrorCode);
        Assert.True(Use(session, AccountB, "full-ended", "STRIKE", ActorA, 23).Succeeded);
    }

    [Fact]
    public void StunRejectsAutoAttackAndAutoAttackResumesAtExpiration()
    {
        ArenaCombatSession session = CreateArena(TimeSpan.FromSeconds(1));
        Assert.True(Use(session, AccountB, "stun-a", "STUN_A", ActorA, 0).Succeeded);

        session.AdvanceTo(Start.AddSeconds(1));
        Assert.Equal(100m, session.Snapshot.ActorB.CurrentHp);
        Assert.Contains(session.GetEventsAfter(0), item =>
            item.Type == CombatEventType.ActionRejected
            && item.ActorId == ActorA
            && item.DefinitionId == "ActorStunned");

        session.AdvanceTo(Start.AddSeconds(4));
        Assert.True(session.Snapshot.ActorB.CurrentHp < 100m);
    }

    [Fact]
    public void StunInterruptsAnActiveCast()
    {
        ArenaCombatSession session = CreateArena();
        Assert.True(Use(session, AccountB, "long-cast", "LONG_CAST", ActorA, 0).Succeeded);
        Assert.True(Use(session, AccountA, "stun-caster", "STUN_A", ActorB, 1).Succeeded);

        session.AdvanceTo(Start.AddSeconds(5));

        Assert.Equal(100m, session.Snapshot.ActorA.CurrentHp);
        Assert.Contains(session.GetEventsAfter(0), item =>
            item.Type == CombatEventType.AbilityInterrupted
            && item.TargetActorId == ActorB);
    }

    [Theory]
    [InlineData("FEAR", "ActorFeared")]
    [InlineData("DISARM", "ActorDisarmed")]
    public void FearAndDisarmPreventAutoAttacksUntilExpiration(string abilityId, string error)
    {
        ArenaCombatSession session = CreateArena(TimeSpan.FromSeconds(1));
        Assert.True(Use(session, AccountA, "control", abilityId, ActorB, 0).Succeeded);
        session.AdvanceTo(Start.AddSeconds(3));
        Assert.Equal(100m, session.Snapshot.ActorA.CurrentHp);
        Assert.Contains(session.GetEventsAfter(0), e => e.ActorId == ActorB
            && e.Type == CombatEventType.ActionRejected && e.DefinitionId == error);
        session.AdvanceTo(Start.AddSeconds(4));
        Assert.True(session.Snapshot.ActorA.CurrentHp < 100m);
    }

    [Fact]
    public void SilenceInterruptsSpellButDoesNotInterruptPhysicalCast()
    {
        ArenaCombatSession spell = CreateArena();
        Assert.True(Use(spell, AccountB, "cast", "MAGE_FIREBALL", ActorA, 0).Succeeded);
        Assert.True(UseAt(spell, AccountA, "silence", "SILENCE", ActorB, Start.AddMilliseconds(500)).Succeeded);
        Assert.Null(spell.ActiveCastFor(AccountB));
        ArenaCombatSession physical = CreateArena();
        Assert.True(Use(physical, AccountB, "cast", "LONG_CAST", ActorA, 0).Succeeded);
        Assert.True(Use(physical, AccountA, "silence", "SILENCE", ActorB, 1).Succeeded);
        Assert.NotNull(physical.ActiveCastFor(AccountB));
    }

    [Fact]
    public void FireballStunSharesTheGenericStunDrWithAnotherAbility()
    {
        ArenaCombatSession session = CreateArena();
        Assert.True(Use(session, AccountA, "fireball", "MAGE_FIREBALL", ActorB, 0).Succeeded);
        session.AdvanceTo(Start.AddSeconds(1));

        Assert.Equal("ActorStunned", UseAt(session, AccountB, "fireball-blocked", "STRIKE", ActorA,
            Start.AddSeconds(4.5)).ErrorCode);
        Assert.True(Use(session, AccountB, "fireball-ended", "STRIKE", ActorA, 5).Succeeded);

        Assert.True(Use(session, AccountA, "other-stun", "STUN_B", ActorB, 5).Succeeded);
        Assert.Equal("ActorStunned", UseAt(session, AccountB, "shared-dr", "STRIKE", ActorA,
            Start.AddSeconds(6.5)).ErrorCode);
        Assert.True(Use(session, AccountB, "shared-dr-ended", "STRIKE", ActorA, 7).Succeeded);
    }

    [Fact]
    public void SharedPveEffectEngineStillUsesNormalEffectDuration()
    {
        CombatActorState target = new(ActorB, 100, 100, 100, 100, CombatStats.Default);
        EffectDefinition stun = new(
            "PVE_STUN",
            EffectKind.Stun,
            TimeSpan.FromSeconds(4),
            1,
            EffectStackPolicy.Replace,
            0);

        EffectEngine.Apply(target, ActorA, stun, Start);
        EffectEngine.Apply(target, ActorA, stun, Start.AddSeconds(4));

        Assert.True(EffectEngine.HasControl(target, EffectKind.Stun, Start.AddSeconds(7.9)));
        Assert.False(EffectEngine.HasControl(target, EffectKind.Stun, Start.AddSeconds(8)));
    }

    private static ArenaCommandResult Use(
        ArenaCombatSession session,
        Guid accountId,
        string commandId,
        string abilityId,
        Guid targetActorId,
        double seconds) =>
        UseAt(session, accountId, commandId, abilityId, targetActorId, Start.AddSeconds(seconds));

    private static ArenaCommandResult UseAt(
        ArenaCombatSession session,
        Guid accountId,
        string commandId,
        string abilityId,
        Guid targetActorId,
        DateTimeOffset at) =>
        session.UseAbility(accountId, commandId, abilityId, targetActorId, at);

    private static ArenaCombatSession CreateArena(TimeSpan? autoAttackInterval = null)
    {
        Dictionary<string, AbilityDefinition> abilities = new(StringComparer.Ordinal)
        {
            ["STRIKE"] = DamageAbility("STRIKE"),
            ["DASH"] = DamageAbility("DASH", requiresMobility: true),
            ["LONG_CAST"] = CastDamageAbility("LONG_CAST", TimeSpan.FromSeconds(5)),
            ["STUN_A"] = ControlAbility("STUN_A", "STUN_A_EFFECT", EffectKind.Stun),
            ["STUN_B"] = ControlAbility("STUN_B", "STUN_B_EFFECT", EffectKind.Stun),
            ["ROOT"] = ControlAbility("ROOT", "ROOT_EFFECT", EffectKind.Root),
            ["FEAR"] = ControlAbility("FEAR", "FEAR_EFFECT", EffectKind.Fear),
            ["DISARM"] = ControlAbility("DISARM", "DISARM_EFFECT", EffectKind.Disarm),
            ["SILENCE"] = ControlAbility("SILENCE", "SILENCE_EFFECT", EffectKind.Silence),
            ["MAGE_FIREBALL"] = FireballAbility()
        };
        AutoAttackProfile auto = new(autoAttackInterval ?? TimeSpan.FromHours(1), 10, 0, 0);
        CombatActorState firstActor = new(ActorA, 100, 100, 100, 100, CombatStats.Default);
        CombatActorState secondActor = new(ActorB, 100, 100, 100, 100, CombatStats.Default);
        return new ArenaCombatSession(
            Guid.NewGuid(),
            new ArenaFighter(AccountA, ActorA, firstActor, abilities, auto),
            new ArenaFighter(AccountB, ActorB, secondActor, abilities, auto),
            new SeededGameRandom(42),
            Start);
    }

    private static AbilityDefinition DamageAbility(string id, bool requiresMobility = false) =>
        new(
            id,
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
                    1,
                    DamageType.Physical,
                    CanMiss: false,
                    CanCrit: false,
                    CanDodge: false)
            ],
            RequiresMobility: requiresMobility);

    private static AbilityDefinition CastDamageAbility(string id, TimeSpan castTime) =>
        new(
            id,
            AbilityType.Casted,
            AbilityTargetType.SingleEnemy,
            0,
            TimeSpan.Zero,
            castTime,
            false,
            GlobalCooldownCategory.None,
            false,
            "Physical",
            Actions:
            [
                new AbilityActionDefinition(
                    AbilityActionType.Damage,
                    25,
                    DamageType.Physical,
                    CanMiss: false,
                    CanCrit: false,
                    CanDodge: false)
            ]);

    private static AbilityDefinition ControlAbility(
        string abilityId,
        string effectId,
        EffectKind kind) =>
        new(
            abilityId,
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
                    AbilityActionType.ApplyEffect,
                    Effect: new EffectDefinition(
                        effectId,
                        kind,
                        TimeSpan.FromSeconds(4),
                        1,
                        EffectStackPolicy.Replace,
                        0))
            ]);

    private static AbilityDefinition FireballAbility() =>
        new(
            "MAGE_FIREBALL",
            AbilityType.Casted,
            AbilityTargetType.SingleEnemy,
            0,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1),
            false,
            GlobalCooldownCategory.None,
            true,
            "FIRE",
            Actions:
            [
                new AbilityActionDefinition(
                    AbilityActionType.Damage,
                    1,
                    DamageType.Magical,
                    CanMiss: false,
                    CanCrit: false,
                    CanDodge: false),
                new AbilityActionDefinition(
                    AbilityActionType.ApplyEffect,
                    Effect: new EffectDefinition(
                        "MAGE_FIRE_IMPACT_STUN",
                        EffectKind.Stun,
                        TimeSpan.FromSeconds(4),
                        1,
                        EffectStackPolicy.Replace,
                        0,
                        SourceSpecific: true))
            ]);
}
