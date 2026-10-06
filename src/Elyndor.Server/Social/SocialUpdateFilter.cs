using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Contracts.Social;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Server.Realtime;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Server.Social;

public sealed class SocialUpdateFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        if (HttpMethods.IsGet(context.HttpContext.Request.Method)
            || !TryGetAccountId(context.HttpContext.User, out Guid accountId))
        {
            return await next(context);
        }

        IServiceProvider services = context.HttpContext.RequestServices;
        GameDbContext db = services.GetRequiredService<GameDbContext>();
        CancellationToken token = context.HttpContext.RequestAborted;
        HashSet<Guid> recipients = [accountId];

        SendFriendRequestRequest? sendRequest =
            context.Arguments.OfType<SendFriendRequestRequest>().FirstOrDefault();
        if (sendRequest is not null)
        {
            await AddAccountForCharacterAsync(
                db,
                sendRequest.TargetCharacterId,
                recipients,
                token);
        }

        if (TryRouteGuid(context.HttpContext, "requestId", out Guid requestId))
        {
            var requestParticipants = await db.FriendRequests
                .AsNoTracking()
                .Where(request => request.Id == requestId)
                .Select(request => new
                {
                    request.RequesterCharacterId,
                    request.TargetCharacterId
                })
                .SingleOrDefaultAsync(token);
            if (requestParticipants is not null)
            {
                await AddAccountsForCharactersAsync(
                    db,
                    [requestParticipants.RequesterCharacterId, requestParticipants.TargetCharacterId],
                    recipients,
                    token);
            }
        }

        if (TryRouteGuid(context.HttpContext, "friendCharacterId", out Guid friendCharacterId))
        {
            await AddAccountForCharacterAsync(
                db,
                friendCharacterId,
                recipients,
                token);
        }

        object? result = await next(context);
        if (result is IStatusCodeHttpResult { StatusCode: >= 400 })
            return result;

        IHubContext<LiveStateHub> hub =
            services.GetRequiredService<IHubContext<LiveStateHub>>();
        await hub.Clients.Groups(recipients.Select(LiveStateHub.GroupName).ToArray())
            .SendAsync("SocialUpdated", cancellationToken: token);

        return result;
    }

    private static bool TryRouteGuid(
        HttpContext context,
        string key,
        out Guid value)
    {
        value = Guid.Empty;
        if (!context.Request.RouteValues.TryGetValue(key, out object? raw) || raw is null)
            return false;

        return Guid.TryParse(raw.ToString(), out value) && value != Guid.Empty;
    }

    private static async Task AddAccountForCharacterAsync(
        GameDbContext db,
        Guid characterId,
        HashSet<Guid> recipients,
        CancellationToken token)
    {
        Guid? accountId = await db.Characters
            .AsNoTracking()
            .Where(character => character.Id == characterId)
            .Select(character => (Guid?)character.AccountId)
            .SingleOrDefaultAsync(token);
        if (accountId.HasValue)
            recipients.Add(accountId.Value);
    }

    private static async Task AddAccountsForCharactersAsync(
        GameDbContext db,
        IReadOnlyCollection<Guid> characterIds,
        HashSet<Guid> recipients,
        CancellationToken token)
    {
        if (characterIds.Count == 0)
            return;

        Guid[] ids = characterIds.ToArray();
        Guid[] accountIds = await db.Characters
            .AsNoTracking()
            .Where(character => ids.Contains(character.Id))
            .Select(character => character.AccountId)
            .ToArrayAsync(token);
        recipients.UnionWith(accountIds);
    }

    private static bool TryGetAccountId(
        ClaimsPrincipal user,
        out Guid accountId) =>
        Guid.TryParse(
            user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub),
            out accountId)
        && accountId != Guid.Empty;
}
