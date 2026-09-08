using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Contracts.Parties;
using Elyndor.Infrastructure.Parties;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Server.Combat;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Server.Parties;

public sealed class PartyUpdateFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (HttpMethods.IsGet(context.HttpContext.Request.Method)
            || !Guid.TryParse(context.HttpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub), out Guid accountId))
            return await next(context);

        IServiceProvider services = context.HttpContext.RequestServices;
        PartyService parties = services.GetRequiredService<PartyService>();
        CancellationToken token = context.HttpContext.RequestAborted;
        HashSet<Guid> recipients = (await parties.GetCombatMembersAsync(accountId, token))
            .Select(member => member.AccountId).ToHashSet();
        InviteToPartyRequest? invite = context.Arguments.OfType<InviteToPartyRequest>().FirstOrDefault();
        if (invite is not null)
        {
            GameDbContext db = services.GetRequiredService<GameDbContext>();
            Guid? target = await db.Characters.Where(character => character.Id == invite.TargetCharacterId)
                .Select(character => (Guid?)character.AccountId).SingleOrDefaultAsync(token);
            if (target.HasValue) recipients.Add(target.Value);
        }

        object? result = await next(context);
        if (result is IStatusCodeHttpResult { StatusCode: >= 400 }) return result;
        recipients.UnionWith((await parties.GetCombatMembersAsync(accountId, token)).Select(member => member.AccountId));
        IHubContext<CombatHub> hub = services.GetRequiredService<IHubContext<CombatHub>>();
        await hub.Clients.Groups(recipients.Select(CombatHub.GroupName).ToArray())
            .SendAsync("PartyUpdated", cancellationToken: token);
        return result;
    }
}
