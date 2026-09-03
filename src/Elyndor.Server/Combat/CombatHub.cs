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
    public async Task<CombatUpdateResponse> StartCombat(string monsterId)
    {
        Guid accountId = GetAccountId();
        CancellationToken cancellationToken = Context.ConnectionAborted;
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(accountId), cancellationToken);
        return CombatContractMapper.ToResponse(
            await combat.StartAsync(accountId, monsterId, cancellationToken));
    }

    public Task<CombatUpdateResponse> ResetTraining() => ToResponseAsync(
        combat.ResetTrainingAsync(GetAccountId(), Context.ConnectionAborted));

    public Task<CombatUpdateResponse> UseAbility(
        Guid sessionId,
        string abilityId,
        string commandId) => ToResponseAsync(
            combat.UseAbilityAsync(
                GetAccountId(), sessionId, commandId, abilityId, Context.ConnectionAborted));

    public Task<CombatUpdateResponse> UseConsumable(
        Guid sessionId,
        string itemDefinitionId,
        string commandId) => ToResponseAsync(
            combat.UseConsumableAsync(
                GetAccountId(), sessionId, commandId, itemDefinitionId, Context.ConnectionAborted));

    public Task<CombatUpdateResponse> StartAutoAttack(
        Guid sessionId,
        string commandId) => ToResponseAsync(
            combat.StartAutoAttackAsync(
                GetAccountId(), sessionId, commandId, Context.ConnectionAborted));

    public Task<CombatUpdateResponse> StopAutoAttack(
        Guid sessionId,
        string commandId) => ToResponseAsync(
            combat.StopAutoAttackAsync(
                GetAccountId(), sessionId, commandId, Context.ConnectionAborted));

    public async Task<CombatUpdateResponse> ResumeCombat()
    {
        Guid accountId = GetAccountId();
        CancellationToken cancellationToken = Context.ConnectionAborted;
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(accountId), cancellationToken);
        return CombatContractMapper.ToResponse(combat.Resume(accountId));
    }

    public Task<CombatUpdateResponse> LeaveCombat() => ToResponseAsync(
        combat.LeaveAsync(GetAccountId(), Context.ConnectionAborted));

    internal static string GroupName(Guid accountId) => $"combat:{accountId:N}";

    private static async Task<CombatUpdateResponse> ToResponseAsync(Task<CombatOperationResult> operation) =>
        CombatContractMapper.ToResponse(await operation);

    private Guid GetAccountId() =>
        Guid.TryParse(Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub), out Guid accountId)
        && accountId != Guid.Empty
            ? accountId
            : throw new HubException("authentication_required");
}
