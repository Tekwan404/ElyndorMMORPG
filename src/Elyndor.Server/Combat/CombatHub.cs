using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Contracts.Combat;
using Elyndor.Infrastructure.Combat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Elyndor.Server.Combat;

[Authorize]
public sealed class CombatHub(CombatApplicationService combat) : Hub
{
    public async Task<CombatUpdateResponse> StartCombat(
        string monsterId,
        CancellationToken cancellationToken)
    {
        Guid accountId = GetAccountId();
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(accountId), cancellationToken);
        return CombatContractMapper.ToResponse(
            await combat.StartAsync(accountId, monsterId, cancellationToken));
    }

    public async Task<CombatUpdateResponse> UseAbility(
        Guid sessionId,
        string abilityId,
        string commandId,
        CancellationToken cancellationToken) => CombatContractMapper.ToResponse(
            await combat.UseAbilityAsync(
                GetAccountId(), sessionId, commandId, abilityId, cancellationToken));

    public async Task<CombatUpdateResponse> StartAutoAttack(
        Guid sessionId,
        string commandId,
        CancellationToken cancellationToken) => CombatContractMapper.ToResponse(
            await combat.StartAutoAttackAsync(
                GetAccountId(), sessionId, commandId, cancellationToken));

    public async Task<CombatUpdateResponse> StopAutoAttack(
        Guid sessionId,
        string commandId,
        CancellationToken cancellationToken) => CombatContractMapper.ToResponse(
            await combat.StopAutoAttackAsync(
                GetAccountId(), sessionId, commandId, cancellationToken));

    public async Task<CombatUpdateResponse> ResumeCombat(CancellationToken cancellationToken)
    {
        Guid accountId = GetAccountId();
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(accountId), cancellationToken);
        return CombatContractMapper.ToResponse(combat.Resume(accountId));
    }

    public async Task<CombatUpdateResponse> LeaveCombat(CancellationToken cancellationToken) =>
        CombatContractMapper.ToResponse(await combat.LeaveAsync(GetAccountId(), cancellationToken));

    internal static string GroupName(Guid accountId) => $"combat:{accountId:N}";

    private Guid GetAccountId() =>
        Guid.TryParse(Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub), out Guid accountId)
        && accountId != Guid.Empty
            ? accountId
            : throw new HubException("authentication_required");
}
