using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class ArcherCompanionCommandTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 13, 2, 0, 0, TimeSpan.Zero);
    private static readonly Guid PlayerId = Guid.Parse("81000000-0000-0000-0000-000000000001");
    private static readonly Guid CompanionId = Guid.Parse("82000000-0000-0000-0000-000000000001");
    private static readonly Guid EnemyId = Guid.Parse("83000000-0000-0000-0000-000000000001");

    [Fact]
    public void GuardianCommandForcesTheEnemyToAttackTheGuardian()
    {
        CombatSession session = CreateSession("ARCHER_GUARDIAN");

        CombatCommandResult command = session.Handle(
            new UseAbilityCommand("guardian-command", "COMMAND_ATTACK", EnemyId),
            Now.AddSeconds(1));
        CombatThreatSnapshot threat = Assert.IsType<CombatThreatSnapshot>(
            session.GetThreatSnapshot(PlayerId, Now.AddSeconds(1)));

        Assert.True(command.Succeeded);
        Assert.Equal(CompanionId, threat.ForcedTargetActorId);
    }

    [Fact]
    public void PredatorCommandAppliesItsBleed()
    {
        CombatSession session = CreateSession("ARCHER_STARTER_PREDATOR");

        CombatCommandResult command = session.Handle(
            new UseAbilityCommand("predator-command", "COMMAND_ATTACK", EnemyId),
            Now.AddSeconds(1));

        Assert.True(command.Succeeded);
        Assert.Contains(command.Events, item => item.Type == CombatEventType.EffectApplied
            && item.DefinitionId == "ARCHER_PREDATOR_COMMAND_BLEED");
    }

    [Fact]
    public void TrapperCommandAppliesAShortStun()
    {
        CombatSession session = CreateSession("ARCHER_TRAPPER");

        CombatCommandResult command = session.Handle(
            new UseAbilityCommand("trapper-command", "COMMAND_ATTACK", EnemyId),
            Now.AddSeconds(1));

        Assert.True(command.Succeeded);
        Assert.Contains(command.Events, item => item.Type == CombatEventType.EffectApplied
            && item.DefinitionId == "ARCHER_TRAPPER_COMMAND_STUN");
    }

    private static CombatSession CreateSession(string companionDefinitionId)
    {
        CombatStats stats = new(20, 100, 0, 0, 1.5m, 0, 0, 0, 0, 20, 0);
        CombatParticipantDefinition player = new(
            new CombatActorState(PlayerId, 1_000, 1_000, 100, 100, stats),
            CombatActorKind.Player, "ARCHER", "Archer", "FOCUS",
            new AutoAttackProfile(TimeSpan.FromSeconds(30), 0, 0, 0),
            new HashSet<string>(["COMMAND_ATTACK"], StringComparer.Ordinal));
        CombatParticipantDefinition companion = new(
            new CombatActorState(CompanionId, 1_000, 1_000, 0, 0, stats),
            CombatActorKind.Companion, companionDefinitionId, "Companion", "NONE",
            new AutoAttackProfile(TimeSpan.FromSeconds(30), 10, 0, 0),
            new HashSet<string>(StringComparer.Ordinal));
        CombatParticipantDefinition enemy = new(
            new CombatActorState(EnemyId, 10_000, 10_000, 0, 0, stats),
            CombatActorKind.Monster, "TRAINING_ENEMY", "Enemy", "NONE",
            new AutoAttackProfile(TimeSpan.FromSeconds(30), 10, 0, 0),
            new HashSet<string>(StringComparer.Ordinal));

        return new CombatSession(
            Guid.CreateVersion7(), player, enemy,
            new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal)
            {
                ["COMMAND_ATTACK"] = new(
                    "COMMAND_ATTACK", AbilityType.Instant, AbilityTargetType.SingleEnemy,
                    20, TimeSpan.FromSeconds(10), TimeSpan.Zero, false,
                    GlobalCooldownCategory.None, false, "PHYSICAL", Actions: [],
                    RuntimeParameters: new Dictionary<string, decimal>(StringComparer.Ordinal)
                    {
                        ["predatorDamageMultiplier"] = 1.5m,
                        ["predatorBleedPercent"] = 30m,
                        ["predatorBleedDurationSeconds"] = 3m,
                        ["guardianTauntDurationSeconds"] = 3m,
                        ["trapperStunDurationSeconds"] = 1m,
                    })
            },
            new MonsterAiProfile("TRAINING_AI", []), ResolvedTalentModifiers.Empty,
            new SequenceGameRandom(Enumerable.Repeat(0.99m, 100).ToArray()), Now,
            companion: companion);
    }
}
