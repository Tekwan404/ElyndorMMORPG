using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Contracts.Parties;
using Elyndor.Core.Parties;
using Elyndor.Infrastructure.Parties;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Server.Combat;
using Elyndor.Server.Realtime;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Server.Parties;

public sealed class PartyUpdateFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        if (HttpMethods.IsGet(context.HttpContext.Request.Method)
            || !Guid.TryParse(
                context.HttpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub),
                out Guid accountId))
        {
            return await next(context);
        }

        IServiceProvider services = context.HttpContext.RequestServices;
        PartyService parties = services.GetRequiredService<PartyService>();
        GameDbContext db = services.GetRequiredService<GameDbContext>();
        CancellationToken token = context.HttpContext.RequestAborted;

        HashSet<Guid> combatRecipients = (await parties.GetCombatMembersAsync(accountId, token))
            .Select(member => member.AccountId)
            .ToHashSet();
        HashSet<Guid> liveRecipients = [accountId];
        await AddCurrentPartyAccountsAsync(db, accountId, liveRecipients, token);

        InviteToPartyRequest? invite =
            context.Arguments.OfType<InviteToPartyRequest>().FirstOrDefault();
        if (invite is not null)
        {
            await AddAccountForCharacterAsync(
                db,
                invite.TargetCharacterId,
                combatRecipients,
                token);
            await AddAccountForCharacterAsync(
                db,
                invite.TargetCharacterId,
                liveRecipients,
                token);
        }

        if (TryRouteGuid(context.HttpContext, "inviteId", out Guid inviteId))
        {
            var inviteState = await db.PartyInvites
                .AsNoTracking()
                .Where(candidate => candidate.Id == inviteId)
                .Select(candidate => new
                {
                    candidate.PartyId,
                    candidate.TargetCharacterId
                })
                .SingleOrDefaultAsync(token);
            if (inviteState is not null)
            {
                await AddPartyAccountsAsync(
                    db,
                    inviteState.PartyId,
                    liveRecipients,
                    token);
                await AddAccountForCharacterAsync(
                    db,
                    inviteState.TargetCharacterId,
                    liveRecipients,
                    token);
            }
        }

        object? result = await next(context);
        if (result is IStatusCodeHttpResult { StatusCode: >= 400 })
            return result;

        combatRecipients.UnionWith(
            (await parties.GetCombatMembersAsync(accountId, token))
                .Select(member => member.AccountId));
        await AddCurrentPartyAccountsAsync(db, accountId, liveRecipients, token);

        IHubContext<CombatHub> combatHub =
            services.GetRequiredService<IHubContext<CombatHub>>();
        await combatHub.Clients.Groups(
                combatRecipients.Select(CombatHub.GroupName).ToArray())
            .SendAsync("PartyUpdated", cancellationToken: token);

        IHubContext<LiveStateHub> liveStateHub =
            services.GetRequiredService<IHubContext<LiveStateHub>>();
        await liveStateHub.Clients.Groups(
                liveRecipients.Select(LiveStateHub.GroupName).ToArray())
            .SendAsync("PartyUpdated", cancellationToken: token);

        return result;
    }

    private static async Task AddCurrentPartyAccountsAsync(
        GameDbContext db,
        Guid accountId,
        HashSet<Guid> recipients,
        CancellationToken token)
    {
        Guid? characterId = await db.Characters
            .AsNoTracking()
            .Where(character => character.AccountId == accountId)
            .Select(character => (Guid?)character.Id)
            .SingleOrDefaultAsync(token);
        if (!characterId.HasValue)
            return;

        Guid? partyId = await db.PartyMembers
            .AsNoTracking()
            .Where(member => member.CharacterId == characterId.Value
                && member.State == PartyMemberState.Active)
            .Select(member => (Guid?)member.PartyId)
            .SingleOrDefaultAsync(token);
        if (partyId.HasValue)
            await AddPartyAccountsAsync(db, partyId.Value, recipients, token);
    }

    private static async Task AddPartyAccountsAsync(
        GameDbContext db,
        Guid partyId,
        HashSet<Guid> recipients,
        CancellationToken token)
    {
        Guid[] characterIds = await db.PartyMembers
            .AsNoTracking()
            .Where(member => member.PartyId == partyId
                && member.State == PartyMemberState.Active)
            .Select(member => member.CharacterId)
            .ToArrayAsync(token);
        if (characterIds.Length == 0)
            return;

        Guid[] accountIds = await db.Characters
            .AsNoTracking()
            .Where(character => characterIds.Contains(character.Id))
            .Select(character => character.AccountId)
            .ToArrayAsync(token);
        recipients.UnionWith(accountIds);
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

    private static bool TryRouteGuid(
        HttpContext context,
        string key,
        out Guid value)
    {
        return context.Request.RouteValues.TryGetValue(key, out object? raw)
            && Guid.TryParse(Convert.ToString(raw), out value)
            && value != Guid.Empty;
    }
}
