using Elyndor.Core.Pvp;
using Elyndor.Infrastructure.Combat;

namespace Elyndor.Infrastructure.Pvp;

public sealed class ArenaTestOptions
{
    public bool Enabled { get; set; }
}

public sealed record ArenaTestOperationResult(bool Succeeded, string? ErrorCode, ArenaTestState? State);

public sealed class ArenaTestService(
    ArenaTestRegistry registry,
    ArenaFighterFactory fighters,
    ICombatActivityReader pveActivity,
    Microsoft.Extensions.Options.IOptions<ArenaTestOptions> options)
{
    public bool Enabled => options.Value.Enabled;

    public ArenaTestState? Status(Guid accountId, long afterSequence = 0) =>
        Enabled ? registry.Get(accountId, afterSequence) : null;

    public async Task<ArenaTestOperationResult> JoinAsync(Guid accountId, CancellationToken cancellationToken)
    {
        if (!Enabled) return Failure("arena_test_disabled");
        if (registry.Get(accountId) is { } existing)
            return new ArenaTestOperationResult(true, null, existing);
        if (pveActivity.HasActiveCombat(accountId)) return Failure("arena_pve_combat_active");

        ArenaFighterResult fighter = await fighters.CreateAsync(accountId, cancellationToken);
        return fighter.Entrant is { } entrant
            ? new ArenaTestOperationResult(true, null, registry.Join(entrant))
            : Failure(fighter.ErrorCode ?? "arena_unsupported_build");
    }

    public ArenaTestOperationResult UseAbility(Guid accountId, Guid matchId, string commandId,
        string abilityId, Guid targetActorId)
    {
        if (!Enabled) return Failure("arena_test_disabled");
        ArenaTestCommandResult result = registry.UseAbility(accountId, matchId, commandId,
            abilityId, targetActorId);
        return new ArenaTestOperationResult(result.Succeeded, result.ErrorCode, result.State);
    }

    public bool Leave(Guid accountId) => Enabled && registry.Leave(accountId);
    public bool Surrender(Guid accountId) => Enabled && registry.Surrender(accountId);

    private static ArenaTestOperationResult Failure(string errorCode) => new(false, errorCode, null);
}
