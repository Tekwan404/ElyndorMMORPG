using Elyndor.Core.Combat.Targeting;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private AbilityTargetCandidate[] BuildMonsterAiTargetCandidates(
        CombatParticipantDefinition enemy,
        DateTimeOffset now,
        out Guid? currentThreatTargetId)
    {
        CombatParticipantDefinition[] hostileCandidates = ActiveEnemyTargetCandidates();
        currentThreatTargetId = hostileCandidates.Length == 0
            ? null
            : ResolveEnemyPrimaryTarget(enemy, now).Actor.ActorId;

        ThreatTable threatTable = _enemyThreatTables[enemy.Actor.ActorId];
        List<AbilityTargetCandidate> candidates = new(
            hostileCandidates.Length + Math.Max(0, _enemies.Count - 1));

        foreach (CombatParticipantDefinition candidate in hostileCandidates)
        {
            Guid actorId = candidate.Actor.ActorId;
            candidates.Add(new AbilityTargetCandidate(
                actorId,
                IsEnemy: true,
                // Elyndor combat is positionless. Until party roles are modeled explicitly,
                // NonTankRandom treats the monster's current forced/threat target as the tank.
                IsTank: currentThreatTargetId == actorId,
                UsesMana: string.Equals(candidate.ResourceType, "MANA", StringComparison.Ordinal),
                CurrentHp: candidate.Actor.CurrentHp,
                MaxHp: candidate.Actor.MaxHp,
                Threat: threatTable.GetThreat(actorId)));
        }

        foreach (CombatParticipantDefinition ally in _enemies)
        {
            if (ally.Actor.ActorId == enemy.Actor.ActorId || ally.Actor.IsDead)
                continue;

            candidates.Add(new AbilityTargetCandidate(
                ally.Actor.ActorId,
                IsEnemy: false,
                IsTank: false,
                UsesMana: string.Equals(ally.ResourceType, "MANA", StringComparison.Ordinal),
                CurrentHp: ally.Actor.CurrentHp,
                MaxHp: ally.Actor.MaxHp));
        }

        return candidates.ToArray();
    }
}
