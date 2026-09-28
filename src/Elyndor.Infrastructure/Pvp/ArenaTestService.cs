using Elyndor.Core.Afk;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Content;
using Elyndor.Core.Pvp;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.World;

namespace Elyndor.Infrastructure.Pvp;

public sealed class ArenaTestOptions
{
    public bool Enabled { get; set; }
}

public sealed record ArenaTestOperationResult(bool Succeeded, string? ErrorCode, ArenaTestState? State);

public sealed class ArenaTestService(
    ArenaTestRegistry registry,
    BootstrapService bootstrap,
    CharacterDerivedStateService derivedStates,
    CombatSessionFactory combatFactory,
    IContentSnapshotProvider content,
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

        GameContentSnapshot snapshot = content.GetCurrent();
        BootstrapSnapshot player = await bootstrap.GetAsync(accountId, snapshot, cancellationToken,
            checkpoint: true);
        if (player.Character is not { } character || player.World is not { } world)
            return Failure("arena_character_not_found");
        if (character.Vitals.CurrentHp <= 0 || world.Travel is not null
            || player.AfkFarm?.Status == AfkFarmStatus.Active)
            return Failure("arena_character_unavailable");

        CharacterDerivedState derived = await derivedStates.ResolveAsync(character.Id,
            character.ClassId, character.Level, snapshot, cancellationToken);
        if (derived.ClassProfile.CombatAutoAttack is null)
            return Failure("arena_unsupported_build");
        var abilities = (snapshot.Package.Abilities ?? [])
            .ToDictionary(x => x.Id, StringComparer.Ordinal);
        try
        {
            var combatPlayer = await combatFactory.CreateArenaPlayerAsync(player, snapshot,
                cancellationToken);
            ArenaTestEntrant entrant = ArenaFighterAssembler.Create(combatPlayer,
                character.Level, abilities, derived.ActiveTalentRanks.Count > 0,
                derived.ActiveCompanionProfile is not null);
            return new ArenaTestOperationResult(true, null, registry.Join(entrant));
        }
        catch (NotSupportedException)
        {
            return Failure("arena_unsupported_build");
        }
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
