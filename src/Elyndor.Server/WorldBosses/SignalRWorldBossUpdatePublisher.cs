using System.Collections.Concurrent;
using Elyndor.Contracts.WorldBosses;
using Elyndor.Infrastructure.WorldBosses;
using Microsoft.AspNetCore.SignalR;

namespace Elyndor.Server.WorldBosses;

public sealed class SignalRWorldBossUpdatePublisher(
    IHubContext<WorldBossHub> hubContext,
    TimeProvider timeProvider) : IWorldBossUpdatePublisher
{
    private static readonly TimeSpan ProgressPublishInterval = TimeSpan.FromSeconds(1);
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _lastProgressPublishedAt = new();

    public Task PublishActivatedAsync(
        Guid spawnId,
        CancellationToken cancellationToken) =>
        hubContext.Clients.All.SendAsync(
            "WorldBossActivated",
            new WorldBossActivatedResponse(spawnId),
            cancellationToken);

    public Task PublishProgressAsync(
        Guid spawnId,
        decimal currentHealth,
        decimal maxHealth,
        int currentPhase,
        bool phaseChanged,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (!phaseChanged
            && _lastProgressPublishedAt.TryGetValue(spawnId, out DateTimeOffset lastPublished)
            && now - lastPublished < ProgressPublishInterval)
        {
            return Task.CompletedTask;
        }

        _lastProgressPublishedAt[spawnId] = now;
        return hubContext.Clients
            .Group(WorldBossHub.SpawnGroupName(spawnId))
            .SendAsync(
                "WorldBossProgressed",
                new WorldBossProgressResponse(
                    spawnId,
                    currentHealth,
                    maxHealth,
                    currentPhase,
                    phaseChanged),
                cancellationToken);
    }
    public Task PublishDefeatedAsync(
        Guid spawnId,
        DateTimeOffset defeatedAtUtc,
        IReadOnlyCollection<Guid> participantAccountIds,
        CancellationToken cancellationToken)
    {
        _lastProgressPublishedAt.TryRemove(spawnId, out _);
        return hubContext.Clients
            .Group(WorldBossHub.SpawnGroupName(spawnId))
            .SendAsync(
                "WorldBossDefeated",
                new WorldBossDefeatedResponse(spawnId, defeatedAtUtc),
                cancellationToken);
    }

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
