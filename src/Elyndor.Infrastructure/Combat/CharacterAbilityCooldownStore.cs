using Elyndor.Core.Combat;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.Combat;

public sealed class CharacterAbilityCooldownStore(GameDbContext dbContext)
{
    public async Task<IReadOnlyDictionary<string, DateTimeOffset>> LoadActiveAsync(
        Guid characterId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        CharacterAbilityCooldown[] rows = await dbContext.CharacterAbilityCooldowns
            .AsNoTracking()
            .Where(state => state.CharacterId == characterId && state.ReadyAtUtc > now)
            .ToArrayAsync(cancellationToken);
        return rows.ToDictionary(
            state => state.AbilityId,
            state => state.ReadyAtUtc,
            StringComparer.Ordinal);
    }

    public async Task ReplaceAsync(
        Guid characterId,
        IReadOnlyDictionary<string, DateTimeOffset> cooldowns,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        CharacterAbilityCooldown[] existing = await dbContext.CharacterAbilityCooldowns
            .Where(state => state.CharacterId == characterId)
            .ToArrayAsync(cancellationToken);
        Dictionary<string, CharacterAbilityCooldown> byAbility = existing
            .ToDictionary(state => state.AbilityId, StringComparer.Ordinal);
        HashSet<string> activeIds = new(StringComparer.Ordinal);

        foreach ((string abilityId, DateTimeOffset readyAtUtc) in cooldowns)
        {
            if (readyAtUtc <= now)
                continue;

            activeIds.Add(abilityId);
            if (byAbility.TryGetValue(abilityId, out CharacterAbilityCooldown? state))
                state.SetReadyAt(readyAtUtc);
            else
                dbContext.CharacterAbilityCooldowns.Add(
                    new CharacterAbilityCooldown(characterId, abilityId, readyAtUtc));
        }

        CharacterAbilityCooldown[] obsolete = existing
            .Where(state => !activeIds.Contains(state.AbilityId))
            .ToArray();
        if (obsolete.Length > 0)
            dbContext.CharacterAbilityCooldowns.RemoveRange(obsolete);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
