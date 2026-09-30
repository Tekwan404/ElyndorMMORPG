using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaGenericTalentStatusIntegrationTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MomentumChangesActualAttackSpeedAfterSuccessfulRageSpend()
    {
        var ability = new AbilityDefinition("WILD_STRIKE", AbilityType.Instant,
            AbilityTargetType.SingleEnemy, 20, TimeSpan.Zero, TimeSpan.Zero,
            false, GlobalCooldownCategory.None, false, "PHYSICAL",
            Actions: [new AbilityActionDefinition(AbilityActionType.Damage, 10)]);
        var hook = new ResolvedTalentEventHook("B-2-4", TalentModifierKeys.OnAbilityUsed,
            1, 4, null, TimeSpan.Zero, false, Duration: TimeSpan.FromSeconds(3));
        (ArenaCombatSession session, ArenaFighter first, ArenaFighter second) =
            Create(Talents(hook), new Dictionary<string, AbilityDefinition> { [ability.Id] = ability });

        ArenaCommandResult result = session.UseAbility(first.AccountId, "rage-spend",
            ability.Id, second.Actor.ActorId, Start);

        Assert.True(result.Succeeded);
        Assert.Equal(1.04m, EffectEngine.CalculateStat(first.Actor,
            EffectStat.AttackSpeed, 1m, Start));
        Assert.Single(session.ActiveEffectsFor(first.AccountId), effect =>
            effect.Definition.Id == "BERSERKER_MOMENTUM_ATTACK_SPEED");
    }

    [Fact]
    public void DevastatingCriticalAutoAttackAppliesOneEnemyVulnerability()
    {
        var hook = new ResolvedTalentEventHook("B-6-2", TalentModifierKeys.OnCriticalHit,
            1, 5, "AUTO_ATTACK", TimeSpan.Zero, false,
            Duration: TimeSpan.FromSeconds(8));
        (ArenaCombatSession session, ArenaFighter first, ArenaFighter second) =
            Create(Talents(hook), new Dictionary<string, AbilityDefinition>());

        session.AdvanceTo(Start.AddSeconds(4));

        Assert.Single(session.ActiveEffectsFor(second.AccountId), effect =>
            effect.Definition.Id == "BERSERKER_DEVASTATING_VULNERABILITY");
        Assert.Equal(1.05m, EffectEngine.CalculateStat(second.Actor,
            EffectStat.IncomingPhysicalDamageMultiplier, 1m, Start.AddSeconds(4)));
    }

    [Fact]
    public void LethalCriticalAutoAttackDoesNotApplyVulnerabilityToDefeatedOpponent()
    {
        var hook = new ResolvedTalentEventHook("B-6-2", TalentModifierKeys.OnCriticalHit,
            1, 5, "AUTO_ATTACK", TimeSpan.Zero, false,
            Duration: TimeSpan.FromSeconds(8));
        (ArenaCombatSession session, _, ArenaFighter second) =
            Create(Talents(hook), new Dictionary<string, AbilityDefinition>());
        second.Actor.ApplyDamage(499);

        session.AdvanceTo(Start.AddSeconds(4));

        Assert.True(second.Actor.IsDead);
        Assert.DoesNotContain(session.ActiveEffectsFor(second.AccountId), effect =>
            effect.Definition.Id == "BERSERKER_DEVASTATING_VULNERABILITY");
    }

    private static (ArenaCombatSession Session, ArenaFighter First, ArenaFighter Second) Create(
        ResolvedTalentModifiers talents, IReadOnlyDictionary<string, AbilityDefinition> abilities)
    {
        CombatStats firstStats = CombatStats.Default with { Accuracy = 100, CriticalChance = 100 };
        CombatStats secondStats = CombatStats.Default with { Dodge = 0 };
        var first = new ArenaFighter(Guid.NewGuid(), Guid.NewGuid(),
            new CombatActorState(Guid.NewGuid(), 500, 500, 100, 100, firstStats),
            abilities, new AutoAttackProfile(TimeSpan.FromSeconds(4), 20, 0, 0), talents);
        var second = new ArenaFighter(Guid.NewGuid(), Guid.NewGuid(),
            new CombatActorState(Guid.NewGuid(), 500, 500, 100, 100, secondStats),
            new Dictionary<string, AbilityDefinition>(),
            new AutoAttackProfile(TimeSpan.FromHours(1), 1, 0, 0));
        return (new ArenaCombatSession(Guid.NewGuid(), first, second,
            new SeededGameRandom(42), Start), first, second);
    }

    private static ResolvedTalentModifiers Talents(params ResolvedTalentEventHook[] hooks) => new(
        new TalentStatModifiers(), new TalentCombatModifiers(),
        new HashSet<string>(StringComparer.Ordinal),
        new Dictionary<string, TalentAbilityModifiers>(StringComparer.Ordinal), hooks, []);
}
