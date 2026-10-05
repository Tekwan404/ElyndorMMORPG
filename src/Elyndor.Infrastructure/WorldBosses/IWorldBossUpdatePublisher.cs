namespace Elyndor.Infrastructure.WorldBosses;

public sealed record WorldBossRewardDelivery(
    Guid AccountId,
    Guid CharacterId,
    decimal Contribution,
    WorldBossSettlementCharacterResult? Reward);

public interface IWorldBossUpdatePublisher
{
    Task PublishActivatedAsync(
        Guid spawnId,
        CancellationToken cancellationToken);

    Task PublishProgressAsync(
        Guid spawnId,
        decimal currentHealth,
        decimal maxHealth,
        int currentPhase,
        bool phaseChanged,
        CancellationToken cancellationToken);

    Task PublishDefeatedAsync(
        Guid spawnId,
        DateTimeOffset defeatedAtUtc,
        IReadOnlyCollection<Guid> participantAccountIds,
        CancellationToken cancellationToken);

    Task PublishSettledAsync(
        Guid spawnId,
        DateTimeOffset settledAtUtc,
        IReadOnlyCollection<WorldBossRewardDelivery> deliveries,
        CancellationToken cancellationToken);
}
