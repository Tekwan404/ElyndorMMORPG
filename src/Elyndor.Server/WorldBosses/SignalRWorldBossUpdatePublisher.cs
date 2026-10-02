using Elyndor.Contracts.WorldBosses;
using Elyndor.Infrastructure.WorldBosses;
using Microsoft.AspNetCore.SignalR;

namespace Elyndor.Server.WorldBosses;

public sealed class SignalRWorldBossUpdatePublisher(
    IHubContext<WorldBossHub> hubContext) : IWorldBossUpdatePublisher
{
    public Task PublishDefeatedAsync(
        Guid spawnId,
        DateTimeOffset defeatedAtUtc,
        IReadOnlyCollection<Guid> participantAccountIds,
        CancellationToken cancellationToken) =>
        hubContext.Clients
            .Group(WorldBossHub.SpawnGroupName(spawnId))
            .SendAsync(
                "WorldBossDefeated",
                new WorldBossDefeatedResponse(spawnId, defeatedAtUtc),
                cancellationToken);

    public async Task PublishSettledAsync(
        Guid spawnId,
        DateTimeOffset settledAtUtc,
        IReadOnlyCollection<WorldBossRewardDelivery> deliveries,
        CancellationToken cancellationToken)
    {
        foreach (WorldBossRewardDelivery delivery in deliveries)
        {
            WorldBossRewardResponse? reward = delivery.Reward is null
                ? null
                : WorldBossContractMapper.ToRewardResponse(spawnId, delivery.Reward);
            var response = new WorldBossRewardsSettledResponse(
                spawnId,
                delivery.Contribution,
                reward is not null,
                reward,
                settledAtUtc);

            await hubContext.Clients
                .Group(WorldBossHub.AccountGroupName(delivery.AccountId))
                .SendAsync("RewardsSettled", response, cancellationToken);
        }
    }

}
