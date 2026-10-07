using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;
using Elyndor.Infrastructure.Content;

namespace Elyndor.UnitTests.Combat;

public sealed class ArcherP1IntegrationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Fact]
    public async Task PerfectMarkEmpowersOnlyTheFirstAimedOrPiercingShot()
    {
        var (session, _, target) = await Create(Hook("M-8-2", "PERFECT_MARK_BURST", 12));
        Use(session, target, "HUNTER_MARK", 0);
        Use(session, target, "QUICK_SHOT", 1);
        decimal first = Damage(Use(session, target, "PIERCING_ARROW", 2));
        decimal second = Damage(Use(session, target, "PIERCING_ARROW", 3));
        Assert.InRange(first, second * 1.12m - 1, second * 1.12m + 1);
    }

    [Theory]
    [InlineData(40)]
    [InlineData(70)]
    [InlineData(100)]
    public async Task ToxicologyCarriesTheAuthoredFractionOfRemainingPoison(decimal percent)
    {
        var (session, _, target) = await Create(Hook("S-3-3", "TOXICOLOGY", 4, percent));
        Use(session, target, "SERPENT_STING", 0);
        decimal first = target.ActiveEffects.Single(e => e.Definition.Id == "ARCHER_SERPENT_STING").Definition.Magnitude;
        Use(session, target, "SERPENT_STING", 1);
        decimal second = target.ActiveEffects.Single(e => e.Definition.Id == "ARCHER_SERPENT_STING").Definition.Magnitude;
        Assert.Equal(first * (1 + percent / 100m), second);
    }

    [Fact]
    public async Task PreparationUsesItsSeparateCapstoneHookOnce()
    {
        var (session, owner, target) = await Create(Hook("S-9-1", "SURVIVAL_MASTER_PREPARATION", 15, 20));
        Use(session, target, "PREPARATION", 0);
        Use(session, target, "FREEZING_TRAP", 1);
        Assert.Equal(65, owner.CurrentResource);
        Assert.Contains(owner.ActiveEffects, e => e.Definition.Id == "ARCHER_SURVIVAL_MASTER_SHOT");
        Use(session, target, "FREEZING_TRAP", 2);
        Assert.Equal(65, owner.CurrentResource);
    }

    [Fact]
    public async Task PerfectDeterrenceDoesNotResetFreezingTrapCooldown()
    {
        var (session, _, target) = await Create(Hook("S-8-3", "PERFECT_DETERRENCE", 1), trapCooldown: true);
        Use(session, target, "FREEZING_TRAP", 0);
        Use(session, target, "DETERRENCE", 1);
        Assert.False(session.Handle(new UseAbilityCommand("repeat", "FREEZING_TRAP", target.ActorId), Now.AddSeconds(2)).Succeeded);
    }

    private static ResolvedTalentEventHook Hook(string talent, string target, decimal value, decimal secondary = 0) =>
        new(talent, TalentModifierKeys.OnAbilityUsed, 1, value, target, TimeSpan.Zero, false, secondary);

    private static CombatCommandResult Use(CombatSession session, CombatActorState target, string ability, int seconds)
    {
        var result = session.Handle(new UseAbilityCommand($"{ability}-{seconds}", ability, target.ActorId), Now.AddSeconds(seconds));
        Assert.True(result.Succeeded, result.ErrorCode);
        return result;
    }

    private static decimal Damage(CombatCommandResult result) => result.Events
        .Where(e => e.Type == CombatEventType.DamageDealt).Sum(e => e.Amount);

    private static async Task<(CombatSession Session, CombatActorState Owner, CombatActorState Target)> Create(
        ResolvedTalentEventHook hook, bool trapCooldown = false)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "content", "package.json")))
            root = root.Parent;
        var package = await GameContentPackageLoader.LoadAsync(Path.Combine(root!.FullName, "content", "package.json"));
        var abilities = package.Abilities!.Where(a => new[] { "HUNTER_MARK", "QUICK_SHOT", "PIERCING_ARROW",
            "SERPENT_STING", "PREPARATION", "FREEZING_TRAP", "DETERRENCE" }.Contains(a.Id))
            .ToDictionary(a => a.Id, a => a with { ResourceCost = 0, Cooldown = trapCooldown && a.Id == "FREEZING_TRAP"
                ? TimeSpan.FromSeconds(30) : TimeSpan.Zero, CastTime = TimeSpan.Zero, GlobalCooldownCategory = GlobalCooldownCategory.None });
        foreach (string id in new[] { "QUICK_SHOT", "PIERCING_ARROW" })
            abilities[id] = abilities[id] with { Actions = [new(AbilityActionType.Damage, 100, DamageType.Physical,
                CanMiss: false, CanDodge: false, CanCrit: false)] };
        var stats = CombatStats.Default with { AttackPower = 100, Accuracy = 100, CriticalChance = 0, Dodge = 0, Armor = 0 };
        var owner = new CombatActorState(Guid.NewGuid(), 10000, 10000, 100, 50, stats);
        var target = new CombatActorState(Guid.NewGuid(), 100000, 100000, 0, 0, stats);
        CombatParticipantDefinition Participant(CombatActorState actor, bool player) => new(actor,
            player ? CombatActorKind.Player : CombatActorKind.Monster, player ? "ARCHER" : "DUMMY", "Test",
            player ? "FOCUS" : "NONE", new(TimeSpan.FromHours(1), 0, 0, 0), abilities.Keys.ToHashSet(), CanAutoAttack: false);
        var talents = ResolvedTalentModifiers.Empty with { EventHooks = [hook] };
        return (new CombatSession(Guid.NewGuid(), Participant(owner, true), Participant(target, false), abilities,
            new MonsterAiProfile("TEST_AI", []), talents, new SequenceGameRandom(Enumerable.Repeat(.99m, 100).ToArray()), Now), owner, target);
    }
}
