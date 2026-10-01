using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;
using Elyndor.Infrastructure.Content;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaCompanionCapabilityTests
{
    [Fact]
    public async Task EveryProductionBeastMasteryNodeAndPetCommandIsExplicitlyClassified()
    {
        var package = await GameContentPackageLoader.LoadAsync(RepositoryContentPath());
        TalentTreeDefinition tree = Assert.Single(package.TalentTrees!, candidate =>
            candidate.ClassId == "ARCHER");
        TalentDefinition[] beastNodes = tree.Nodes.Where(node =>
            node.BranchId == "BEAST_MASTERY").ToArray();
        Assert.Equal(32, beastNodes.Length);

        foreach (TalentDefinition node in beastNodes)
        {
            foreach (TalentModifierDefinition modifier in node.Modifiers ?? [])
            {
                if (modifier.Type == TalentModifierType.EventTriggered)
                {
                    var probe = new ResolvedTalentEventHook(node.Id, modifier.Key,
                        1, 1, modifier.TargetId, TimeSpan.Zero, false);
                    Assert.Equal(node.Id != "B-1-3",
                        ArenaCompanionCapability.RequiresCompanion(probe));
                }
                else if (modifier.Type == TalentModifierType.AbilityModifier)
                {
                    Assert.True(ArenaCompanionCapability.RequiresCompanion(modifier.TargetId!),
                        $"Unclassified pet command {modifier.TargetId} from {node.Id}.");
                }
                else
                {
                    Assert.Fail($"Unclassified Beast Mastery modifier {node.Id}: {modifier.Type}.");
                }
            }
        }
    }

    [Fact]
    public void SelectedPveCompanionDoesNotBlockArcherOrActivatePetOnlyTalent()
    {
        ResolvedTalentModifiers talents = Talents(new ResolvedTalentEventHook(
            "B-1-1", TalentModifierKeys.OnPartyEvent, 1, 3, "PET_MAX_HP",
            TimeSpan.Zero, false));
        CombatPlayerDefinition player = Archer(talents, "SHOT", "COMMAND_ATTACK");
        var abilities = new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal)
        {
            ["SHOT"] = Shot(),
            ["COMMAND_ATTACK"] = new AbilityDefinition("COMMAND_ATTACK", AbilityType.Instant,
                AbilityTargetType.SingleEnemy, 20, TimeSpan.Zero, TimeSpan.Zero,
                false, GlobalCooldownCategory.None, false, "PHYSICAL",
                RuntimeParameters: new Dictionary<string, decimal> { ["predatorDamageMultiplier"] = 1.5m })
        };

        ArenaTestEntrant entrant = ArenaFighterAssembler.Create(player, 20, abilities,
            hasCompanion: true);

        Assert.Equal(["SHOT"], entrant.Fighter.Abilities.Keys);
        Assert.Empty(entrant.Fighter.EffectiveTalentModifiers.EventHooks);
    }

    [Fact]
    public void UnknownBeastHookIsPreservedForAuditAndRejectedAtAdmission()
    {
        ResolvedTalentModifiers talents = Talents(new ResolvedTalentEventHook(
            "B-99-9", TalentModifierKeys.OnPartyEvent, 1, 3, "UNKNOWN_PET_EFFECT",
            TimeSpan.Zero, false));

        ResolvedTalentModifiers retained = ArenaTalentRuntimeSupport.ForOneVsOne(talents);
        Assert.Contains(retained.EventHooks, hook => hook.TalentId == "B-99-9");
        Assert.Contains(ArenaTalentRuntimeSupport.UnsupportedEventHooks(retained, "ARCHER"),
            hook => hook.TalentId == "B-99-9");
        Assert.Throws<NotSupportedException>(() => ArenaFighterAssembler.Create(
            Archer(talents, "SHOT"), 20,
            new Dictionary<string, AbilityDefinition> { ["SHOT"] = Shot() },
            hasCompanion: true));
    }

    [Fact]
    public void CompanionOnlyHookIsExplicitlyExcludedFromOneVsOne()
    {
        ResolvedTalentModifiers talents = Talents(new ResolvedTalentEventHook(
            "B-1-1", TalentModifierKeys.OnPartyEvent, 1, 3, "PET_MAX_HP",
            TimeSpan.Zero, false));

        ArenaTestEntrant entrant = ArenaFighterAssembler.Create(
            Archer(talents, "SHOT"), 20,
            new Dictionary<string, AbilityDefinition> { ["SHOT"] = Shot() },
            hasCompanion: true);

        Assert.Empty(entrant.Fighter.EffectiveTalentModifiers.EventHooks);
    }

    private static CombatPlayerDefinition Archer(ResolvedTalentModifiers talents,
        params string[] knownAbilities)
    {
        var actor = new CombatActorState(Guid.NewGuid(), 100, 100, 100, 100, CombatStats.Default);
        var participant = new CombatParticipantDefinition(actor, CombatActorKind.Player,
            "ARCHER", "Archer", "FOCUS", new AutoAttackProfile(TimeSpan.FromSeconds(3), 5, 0, 0),
            new HashSet<string>(knownAbilities, StringComparer.Ordinal));
        return new CombatPlayerDefinition(Guid.NewGuid(), participant, talents);
    }

    private static ResolvedTalentModifiers Talents(params ResolvedTalentEventHook[] hooks) => new(
        new TalentStatModifiers(), new TalentCombatModifiers(),
        new HashSet<string>(StringComparer.Ordinal),
        new Dictionary<string, TalentAbilityModifiers>(StringComparer.Ordinal),
        hooks, []);

    private static AbilityDefinition Shot() => new("SHOT", AbilityType.Instant,
        AbilityTargetType.SingleEnemy, 0, TimeSpan.Zero, TimeSpan.Zero,
        false, GlobalCooldownCategory.None, false, "PHYSICAL",
        Actions: [new AbilityActionDefinition(AbilityActionType.Damage, 10)]);

    private static string RepositoryContentPath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "content", "package.json");
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository content package was not found.");
    }
}
