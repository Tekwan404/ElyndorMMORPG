using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Infrastructure.Combat;

namespace Elyndor.Server.Combat;

internal sealed record BossCombatLogTarget(
    string DefinitionId,
    string DisplayName,
    bool IsTrainingDummy,
    bool IsBoss = true);

internal static class BossCombatLogPolicy
{
    public const string IneligibleErrorCode = "combat_log_not_boss";

    // Standard archives also support ordinary encounters. Keep TryResolve
    // boss-only for existing gameplay and automatic report eligibility.
    public static bool TryResolveArchive(
        CombatSessionSnapshot snapshot,
        out BossCombatLogTarget target)
    {
        if (TryResolve(snapshot, out target))
            return true;

        CombatActorSnapshot enemy = (snapshot.Enemies ?? [snapshot.Enemy]).First();
        target = new(
            enemy.DefinitionId,
            string.IsNullOrWhiteSpace(enemy.Name) ? enemy.DefinitionId : enemy.Name,
            false,
            false);
        return true;
    }

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
