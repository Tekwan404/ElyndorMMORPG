using Elyndor.Core.Dungeons;
using Elyndor.Core.Pvp;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.Pvp;

public sealed record ArenaEligibilityResult(ArenaTestEntrant? Entrant, string? ErrorCode)
{
    public bool Eligible => Entrant is not null;
}

/// <summary>
/// Single admission check used both when joining the queue and when a match starts:
/// alive, not travelling, not AFK, not in PvE combat, not in a dungeon, not already in an arena.
/// </summary>
public sealed class ArenaEligibilityService(
    GameDbContext db,
    ICombatActivityReader pveActivity,
    ArenaMatchRuntime runtime,
    ArenaTestRegistry testRegistry,
    ArenaFighterFactory fighters)
{
    public async Task<ArenaEligibilityResult> CheckAsync(Guid accountId, CancellationToken cancellationToken)
    {
        if (runtime.IsInMatch(accountId) || testRegistry.Get(accountId) is not null)
            return new ArenaEligibilityResult(null, "arena_match_active");
        if (pveActivity.HasActiveCombat(accountId))
            return new ArenaEligibilityResult(null, "arena_pve_combat_active");

        Guid characterId = await db.Characters.AsNoTracking()
            .Where(x => x.AccountId == accountId).Select(x => x.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (characterId == Guid.Empty)
            return new ArenaEligibilityResult(null, "arena_character_not_found");
        bool inDungeon = await db.DungeonRuns.AsNoTracking().AnyAsync(run =>
            run.State == DungeonRunState.Active
            && run.Members.Any(member => member.CharacterId == characterId
                && member.State == DungeonRunMemberState.Active), cancellationToken);
        if (inDungeon) return new ArenaEligibilityResult(null, "arena_dungeon_active");

        ArenaFighterResult fighter = await fighters.CreateAsync(accountId, cancellationToken);
        return new ArenaEligibilityResult(fighter.Entrant, fighter.ErrorCode);
    }
}
