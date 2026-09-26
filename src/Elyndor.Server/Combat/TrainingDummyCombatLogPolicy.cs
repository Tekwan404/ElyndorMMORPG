using Elyndor.Infrastructure.Combat;

namespace Elyndor.Server.Combat;

internal static class TrainingDummyCombatLogPolicy
{
    public const string DisplayName = "Тренировочный манекен";
    public const string IneligibleErrorCode = "combat_log_not_training_dummy";

    public static bool IsEligible(IEnumerable<string?> enemyDefinitionIds) =>
        enemyDefinitionIds.Any(definitionId => string.Equals(
            definitionId,
            CombatSessionFactory.TrainingDummyId,
            StringComparison.Ordinal));
}
