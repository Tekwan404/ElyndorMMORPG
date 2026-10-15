using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Contracts.Raids;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Server.Combat;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Server.Raids;

public sealed class RaidUpdateFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        if (HttpMethods.IsGet(context.HttpContext.Request.Method)
            || !Guid.TryParse(
                context.HttpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub),
                out Guid accountId))
            return await next(context);

        IServiceProvider services = context.HttpContext.RequestServices;
        GameDbContext db = services.GetRequiredService<GameDbContext>();
        CancellationToken token = context.HttpContext.RequestAborted;
        HashSet<Guid> recipients = [];
        await AddActorAndRaidRecipientsAsync(db, accountId, recipients, token);

        InviteToRaidRequest? inviteRequest = context.Arguments
            .OfType<InviteToRaidRequest>()
            .FirstOrDefault();
        if (inviteRequest is not null)
            await AddCharacterAccountAsync(db, inviteRequest.TargetCharacterId, recipients, token);

        if (context.HttpContext.Request.RouteValues.TryGetValue("characterId", out object? characterValue)
            && Guid.TryParse(characterValue?.ToString(), out Guid targetCharacterId))
            await AddCharacterAccountAsync(db, targetCharacterId, recipients, token);

        if (context.HttpContext.Request.RouteValues.TryGetValue("inviteId", out object? inviteValue)
            && Guid.TryParse(inviteValue?.ToString(), out Guid inviteId))
            await AddInviteRecipientsAsync(db, inviteId, recipients, token);

        object? result = await next(context);
        if (result is IStatusCodeHttpResult { StatusCode: >= 400 })
            return result;

        await AddActorAndRaidRecipientsAsync(db, accountId, recipients, token);
        if (recipients.Count == 0)
            return result;

        IHubContext<CombatHub> hub = services.GetRequiredService<IHubContext<CombatHub>>();
        await hub.Clients.Groups(recipients.Select(CombatHub.GroupName).ToArray())
            .SendAsync("RaidUpdated", cancellationToken: token);
        return result;
    }

    private static async Task AddActorAndRaidRecipientsAsync(
        GameDbContext db,
        Guid accountId,
        HashSet<Guid> recipients,
        CancellationToken cancellationToken)
    {
        recipients.Add(accountId);
        Guid? characterId = await db.Characters
            .AsNoTracking()
            .Where(character => character.AccountId == accountId)
            .Select(character => (Guid?)character.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (!characterId.HasValue)
            return;

        Guid? raidId = await db.RaidMembers
            .AsNoTracking()
            .Where(member => member.CharacterId == characterId.Value)
            .Select(member => (Guid?)member.RaidId)
            .SingleOrDefaultAsync(cancellationToken);
        if (raidId.HasValue)
            await AddRaidRecipientsAsync(db, raidId.Value, recipients, cancellationToken);
    }

    private static async Task AddInviteRecipientsAsync(
        GameDbContext db,
        Guid inviteId,
        HashSet<Guid> recipients,
        CancellationToken cancellationToken)
    {
        Guid? raidId = await db.RaidInvites
            .AsNoTracking()
            .Where(invite => invite.Id == inviteId)
            .Select(invite => (Guid?)invite.RaidId)
            .SingleOrDefaultAsync(cancellationToken);
        Guid? targetCharacterId = await db.RaidInvites
            .AsNoTracking()
            .Where(invite => invite.Id == inviteId)
            .Select(invite => (Guid?)invite.TargetCharacterId)
            .SingleOrDefaultAsync(cancellationToken);

        if (raidId.HasValue)
            await AddRaidRecipientsAsync(db, raidId.Value, recipients, cancellationToken);
        if (targetCharacterId.HasValue)
            await AddCharacterAccountAsync(db, targetCharacterId.Value, recipients, cancellationToken);
    }

    private static async Task AddRaidRecipientsAsync(
        GameDbContext db,
        Guid raidId,
        HashSet<Guid> recipients,
        CancellationToken cancellationToken)
    {
        Guid[] accountIds = await db.RaidMembers
            .AsNoTracking()
            .Where(member => member.RaidId == raidId)
            .Join(
                db.Characters.AsNoTracking(),
                member => member.CharacterId,
                character => character.Id,
                (_, character) => character.AccountId)
            .ToArrayAsync(cancellationToken);
        recipients.UnionWith(accountIds);
    }

    private static async Task AddCharacterAccountAsync(
        GameDbContext db,
        Guid characterId,
        HashSet<Guid> recipients,
        CancellationToken cancellationToken)
    {
        Guid? accountId = await db.Characters
            .AsNoTracking()
            .Where(character => character.Id == characterId)
            .Select(character => (Guid?)character.AccountId)
            .SingleOrDefaultAsync(cancellationToken);
        if (accountId.HasValue)
            recipients.Add(accountId.Value);
    }
}
