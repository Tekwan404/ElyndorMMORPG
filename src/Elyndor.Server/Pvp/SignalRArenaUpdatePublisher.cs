using Elyndor.Contracts.Arena;
using Elyndor.Infrastructure.Pvp;
using Microsoft.AspNetCore.SignalR;

namespace Elyndor.Server.Pvp;

public sealed class SignalRArenaUpdatePublisher(IHubContext<ArenaHub> hubContext) : IArenaUpdatePublisher
{
    public Task PublishMatchFoundAsync(Guid accountId, Guid matchId, CancellationToken cancellationToken) =>
        hubContext.Clients.Group(ArenaHub.GroupName(accountId))
            .SendAsync("ArenaMatchFound", new ArenaMatchNotification(matchId, 0), cancellationToken);

    public Task PublishMatchUpdatedAsync(Guid accountId, Guid matchId, long sequence,
        CancellationToken cancellationToken) =>
        hubContext.Clients.Group(ArenaHub.GroupName(accountId))
            .SendAsync("ArenaMatchUpdated", new ArenaMatchNotification(matchId, sequence), cancellationToken);
}
