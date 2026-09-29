using Elyndor.Core.Afk;
using Elyndor.Core.Content;
using Elyndor.Core.Pvp;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.World;

namespace Elyndor.Infrastructure.Pvp;

public sealed record ArenaFighterResult(ArenaTestEntrant? Entrant, string? ErrorCode)
{
    public bool Succeeded => Entrant is not null;
}

/// <summary>
/// Builds an arena fighter from the character's real state. Unsupported builds are rejected
/// with an explicit error instead of being silently degraded.
/// </summary>
public sealed class ArenaFighterFactory(
    BootstrapService bootstrap,
    CharacterDerivedStateService derivedStates,
    CombatSessionFactory combatFactory,
    IContentSnapshotProvider content)
{
    public async Task<ArenaFighterResult> CreateAsync(Guid accountId, CancellationToken cancellationToken)
    {
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
            ArenaTestEntrant entrant = ArenaFighterAssembler.Create(combatPlayer, character.Level,
                abilities, derived.ActiveTalentRanks.Count > 0,
                derived.ActiveCompanionProfile is not null);
            return new ArenaFighterResult(entrant, null);
        }
        catch (NotSupportedException)
        {
            return Failure("arena_unsupported_build");
        }
    }

    private static ArenaFighterResult Failure(string code) => new(null, code);
}
