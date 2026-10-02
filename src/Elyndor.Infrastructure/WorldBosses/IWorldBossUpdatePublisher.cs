namespace Elyndor.Infrastructure.WorldBosses;

public sealed record WorldBossRewardDelivery(
    Guid AccountId,
    Guid CharacterId,
    decimal Contribution,
    WorldBossSettlementCharacterResult? Reward);

public interface IWorldBossUpdatePublisher
{
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
