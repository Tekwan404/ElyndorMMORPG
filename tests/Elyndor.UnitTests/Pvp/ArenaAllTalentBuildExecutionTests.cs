using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;
using Elyndor.Infrastructure.Content;
using Xunit.Abstractions;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaAllTalentBuildExecutionTests(ITestOutputHelper output)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Lazy<Task<GameContentPackage>> Content = new(() =>
        GameContentPackageLoader.LoadAsync(ContentPath()));

    [Theory]
    [InlineData("ARCHER")]
    [InlineData("MAGE")]
    [InlineData("PALADIN")]
    [InlineData("WARRIOR")]
    public async Task EveryIndividualMaxRankNodeEntersRuntimeAndExecutesAvailableAbilities(string classId)
    {
        GameContentPackage package = await Content.Value;
        TalentTreeDefinition tree = Assert.Single(package.TalentTrees ?? [], t => t.ClassId == classId);
        var failures = new List<string>();
        var counts = new SweepCounts();
        foreach (TalentDefinition node in tree.Nodes.OrderBy(n => n.Id, StringComparer.Ordinal))
        {
            ResolvedTalentModifiers talents = TalentModifierResolver.Resolve(tree,
                new Dictionary<string, int>(StringComparer.Ordinal) { [node.Id] = node.MaxRank });
            ExerciseBuild(package, classId, $"{node.BranchId}/{node.Id}", talents, counts, failures);
        }

        Report(classId, "individual nodes", tree, counts);
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Theory]
    [InlineData("ARCHER")]
    [InlineData("MAGE")]
    [InlineData("PALADIN")]
    [InlineData("WARRIOR")]
    public async Task FullyComposedBranchesAndTreeRetainOwnerHooksAndExecuteAbilities(string classId)
    {
        GameContentPackage package = await Content.Value;
        TalentTreeDefinition tree = Assert.Single(package.TalentTrees ?? [], t => t.ClassId == classId);
        var failures = new List<string>();
        var counts = new SweepCounts();
        foreach (IGrouping<string, TalentDefinition> branch in tree.Nodes.GroupBy(n => n.BranchId))
        {
            ExerciseBuild(package, classId, branch.Key,
                TalentModifierResolver.Resolve(tree, branch.ToDictionary(n => n.Id, n => n.MaxRank,
                    StringComparer.Ordinal)), counts, failures);
        }

        // These are runtime stress builds, intentionally beyond the learning budget.
        ExerciseBuild(package, classId, "ALL_BRANCHES",
            TalentModifierResolver.Resolve(tree, tree.Nodes.ToDictionary(n => n.Id, n => n.MaxRank,
                StringComparer.Ordinal)), counts, failures);
        Report(classId, "composed builds", tree, counts);
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static void ExerciseBuild(GameContentPackage package, string classId, string label,
        ResolvedTalentModifiers talents, SweepCounts counts, List<string> failures)
    {
        counts.Builds++;
        counts.Hooks += talents.EventHooks.Count(h => !ArenaCompanionCapability.RequiresCompanion(h));
        counts.CompanionHooks += talents.EventHooks.Count(ArenaCompanionCapability.RequiresCompanion);
        ClassProfile profile = Assert.Single(package.ClassProfiles ?? [], p => p.Id == classId);
        Dictionary<string, AbilityDefinition> abilities = (package.Abilities ?? [])
            .ToDictionary(a => a.Id, StringComparer.Ordinal);
        string baseline = classId switch
        {
            "ARCHER" => "PIERCING_ARROW",
            "MAGE" => "MAGE_FIREBALL",
            "PALADIN" => "JUDGEMENT",
            "WARRIOR" => "STRIKE",
            _ => throw new ArgumentOutOfRangeException(nameof(classId))
        };
        string[] known = (profile.StartingAbilityIds ?? []).Append(baseline)
            .Distinct(StringComparer.Ordinal).ToArray();
        string[] executableIds = known.Concat(talents.UnlockedAbilityIds)
            .Where(id => !ArenaCompanionCapability.RequiresCompanion(id))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        counts.Abilities.UnionWith(executableIds);
        foreach (string abilityId in executableIds)
        {
            counts.Attempts++;
            Exception? failure = Record.Exception(() =>
            {
                Assert.Empty(talents.DeferredHooks);
                Assert.True(abilities.TryGetValue(abilityId, out AbilityDefinition? ability),
                    $"Production ability {abilityId} is missing.");
                ArenaFighter first = Assemble(profile, talents, known, abilities);
                ArenaFighter second = Assemble(profile, talents, known, abilities);
                var session = new ArenaCombatSession(Guid.NewGuid(), first, second,
                    new SequenceGameRandom(Enumerable.Repeat(0.5m, 10000).ToArray()), Now);
                ResolvedTalentEventHook[] expectedHooks = talents.EventHooks
                    .Where(h => !ArenaCompanionCapability.RequiresCompanion(h)).ToArray();
                Assert.Equal(expectedHooks, first.EffectiveTalentModifiers.EventHooks);
                Assert.Equal(expectedHooks, second.EffectiveTalentModifiers.EventHooks);
                foreach (string unlocked in talents.UnlockedAbilityIds
                             .Where(id => !ArenaCompanionCapability.RequiresCompanion(id)))
                {
                    Assert.True(first.Abilities.ContainsKey(unlocked), $"Assembler dropped {unlocked}.");
                    Assert.True(second.Abilities.ContainsKey(unlocked), $"Assembler dropped {unlocked}.");
                }

                Guid targetId = ability!.TargetType is AbilityTargetType.Self or AbilityTargetType.SingleAlly
                    or AbilityTargetType.SelfAndPartyMembersInCombat
                    ? first.Actor.ActorId : second.Actor.ActorId;
                ArenaCommandResult result = session.UseAbility(first.AccountId, "sweep-cast", abilityId, targetId, Now);
                if (abilityId == "INTERCESSION")
                {
                    Assert.False(result.Succeeded);
                    Assert.Equal("arena_invalid_target", result.ErrorCode);
                    counts.ExpectedUnavailable++;
                }
                else if (!result.Succeeded && ability.RequiredActiveEffectId is { Length: > 0 } required
                         && !first.Actor.ActiveEffects.Any(e => e.Definition.Id == required && e.ExpiresAtUtc > Now))
                {
                    Assert.Equal(nameof(AbilityErrorCode.AbilityUnavailable), result.ErrorCode);
                    counts.ExpectedUnavailable++;
                }
                else
                {
                    Assert.True(result.Succeeded, $"{abilityId} rejected: {result.ErrorCode}");
                    Assert.Contains(result.Events, e => e.Type == CombatEventType.AbilityStarted
                        && e.DefinitionId == abilityId && e.SourceActorId == first.Actor.ActorId);
                    counts.Succeeded++;
                }

                session.AdvanceTo(Now + ability.CastTime + TimeSpan.FromSeconds(8));
                Assert.Null(session.ActiveCastFor(first.AccountId));
                if (result.Succeeded)
                    Assert.Contains(session.GetEventsAfter(0), e => e.Type == CombatEventType.AbilityCompleted
                        && e.DefinitionId == abilityId && e.SourceActorId == first.Actor.ActorId);
                Assert.InRange(first.Actor.CurrentResource, 0, first.Actor.MaxResource);
                Assert.InRange(second.Actor.CurrentResource, 0, second.Actor.MaxResource);
                Assert.InRange(first.Actor.CurrentHp, 0, first.Actor.MaxHp);
                Assert.InRange(second.Actor.CurrentHp, 0, second.Actor.MaxHp);
            });
            if (failure is not null)
                failures.Add($"{classId}/{label}/{abilityId}: {failure.GetType().Name}: {failure.Message}");
        }
    }

    private static ArenaFighter Assemble(ClassProfile profile, ResolvedTalentModifiers talents,
        IReadOnlyCollection<string> known, IReadOnlyDictionary<string, AbilityDefinition> abilities)
    {
        var stats = new CombatStats(60, 100, 0, 60, 1.5m, 100, 100, 0.1m, 0.1m,
            AttackPower: 200, SpellPower: 200, BlockChance: 60, BlockValueMin: 20, BlockValueMax: 40);
        var actor = new CombatActorState(Guid.NewGuid(), 1000000, 500000, 100000, 50000,
            stats, talents.Combat);
        var participant = new CombatParticipantDefinition(actor, CombatActorKind.Player, profile.Id,
            "Arena sweep", profile.ResourceProfileId,
            profile.CombatAutoAttack ?? new AutoAttackProfile(TimeSpan.FromSeconds(2), 10, 0.5m, 3),
            new HashSet<string>(known, StringComparer.Ordinal), ResourceRegenPerSecond: 5);
        return ArenaFighterAssembler.Create(new CombatPlayerDefinition(Guid.NewGuid(), participant, talents),
            60, abilities, hasCompanion: false).Fighter;
    }

    private void Report(string classId, string scope, TalentTreeDefinition tree, SweepCounts counts) =>
        output.WriteLine($"{classId} {scope}: branches={tree.Nodes.Select(n => n.BranchId).Distinct().Count()}, "
            + $"nodes={tree.Nodes.Count}, builds={counts.Builds}, ownerHookOccurrences={counts.Hooks}, "
            + $"companionHookOccurrencesExcluded={counts.CompanionHooks}, uniqueActiveAbilities={counts.Abilities.Count}, "
            + $"attempts={counts.Attempts}, succeeded={counts.Succeeded}, expectedUnavailable={counts.ExpectedUnavailable}");

    private static string ContentPath()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, "content", "package.json");
            if (File.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException("Repository content package was not found.");
    }

    private sealed class SweepCounts
    {
        public int Builds;
        public int Hooks;
        public int CompanionHooks;
        public int Attempts;
        public int Succeeded;
        public int ExpectedUnavailable;
        public HashSet<string> Abilities { get; } = new(StringComparer.Ordinal);
    }
}
