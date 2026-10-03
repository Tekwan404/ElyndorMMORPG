using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Infrastructure.Combat;

namespace Elyndor.Server.Combat;

internal sealed record BossCombatLogTarget(
    string DefinitionId,
    string DisplayName,
    bool IsTrainingDummy);

internal static class BossCombatLogPolicy
{
    public const string IneligibleErrorCode = "combat_log_not_boss";

    public static bool TryResolve(
        CombatSessionSnapshot snapshot,
        out BossCombatLogTarget target)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        foreach (CombatActorSnapshot enemy in snapshot.Enemies ?? [snapshot.Enemy])
        {
            if (string.Equals(
                    enemy.DefinitionId,
                    CombatSessionFactory.TrainingDummyId,
                    StringComparison.Ordinal))
            {
                target = new(
                    enemy.DefinitionId,
                    TrainingDummyCombatLogPolicy.DisplayName,
                    true);
                return true;
            }

            if (enemy.MonsterRank == MonsterRank.Boss)
            {
                target = new(
                    enemy.DefinitionId,
                    string.IsNullOrWhiteSpace(enemy.Name)
                        ? enemy.DefinitionId
                        : enemy.Name,
                    false);
                return true;
            }
        }

        target = null!;
        return false;
    }
}
