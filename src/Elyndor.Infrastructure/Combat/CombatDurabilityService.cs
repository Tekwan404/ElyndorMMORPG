using Elyndor.Core.Characters;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Elyndor.Infrastructure.Combat;

public sealed class CombatDurabilityService(
    GameDbContext dbContext,
    ILogger<CombatDurabilityService> logger)
{
    public async Task<bool> BeginAsync(
        Guid characterId,
        CombatSessionSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        if (characterId == Guid.Empty)
            throw new ArgumentException("Character id cannot be empty.", nameof(characterId));

        ActiveCombatSession? existing = await dbContext.ActiveCombatSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                state => state.CharacterId == characterId,
                cancellationToken);
        if (existing is not null)
            return existing.SessionId == snapshot.SessionId;

        dbContext.ActiveCombatSessions.Add(new ActiveCombatSession(
            snapshot.SessionId,
            characterId,
            snapshot.ServerTimeUtc,
            snapshot.ContentVersion,
            snapshot.BalanceVersion));
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task CompleteAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        await dbContext.ActiveCombatSessions
            .Where(state => state.SessionId == sessionId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<string?> ReserveConsumableAsync(
        Guid accountId,
        Guid sessionId,
        string commandId,
        string itemDefinitionId,
        GameContentSnapshot contentSnapshot,
        DateTimeOffset usedAtUtc,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(commandId)
            || commandId.Length > 128)
        {
            return Inventory.InventoryErrorCodes.Conflict;
        }

        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using IDbContextTransaction transaction =
                await dbContext.Database.BeginTransactionAsync(cancellationToken);

            Character? character = await dbContext.Characters
                .FromSqlInterpolated(
                    $"SELECT * FROM game.characters WHERE \"AccountId\" = {accountId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (character is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Inventory.InventoryErrorCodes.CharacterNotFound;
            }

            ActiveCombatSession? active = await dbContext.ActiveCombatSessions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    state => state.SessionId == sessionId
                        && state.CharacterId == character.Id,
                    cancellationToken);
            if (active is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Inventory.InventoryErrorCodes.Conflict;
            }

            CombatConsumableUse? existing = await dbContext.CombatConsumableUses
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    use => use.SessionId == sessionId
                        && use.CommandId == commandId,
                    cancellationToken);
            if (existing is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return string.Equals(
                    existing.ItemDefinitionId,
                    itemDefinitionId,
                    StringComparison.Ordinal)
                        ? null
                        : Inventory.InventoryErrorCodes.MutationConflict;
            }

            if (!contentSnapshot.Indexes.ItemsById.TryGetValue(
                    itemDefinitionId,
                    out ItemDefinition? definition)
                || definition.Type != ItemType.Consumable
                || !definition.Stackable
                || definition.MaxStack < 2)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Inventory.InventoryErrorCodes.NotConsumable;
            }

            CharacterItem? item = await dbContext.CharacterItems
                .Where(candidate =>
                    candidate.CharacterId == character.Id
                    && candidate.ItemDefinitionId == itemDefinitionId
                    && candidate.DefinitionVersion == definition.Version
                    && candidate.Quantity > 0)
                .OrderBy(candidate => candidate.AcquiredAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
            if (item is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Inventory.InventoryErrorCodes.ItemNotFound;
            }

            item.RemoveQuantity(1);
            if (item.Quantity == 0)
                dbContext.CharacterItems.Remove(item);

            dbContext.CombatConsumableUses.Add(new CombatConsumableUse(
                sessionId,
                commandId,
                character.Id,
                definition.Id,
                definition.Version,
                definition.MaxStack,
                usedAtUtc));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        });
    }

    public async Task RollbackConsumableAsync(
        Guid sessionId,
        string commandId,
        CancellationToken cancellationToken)
    {
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using IDbContextTransaction transaction =
                await dbContext.Database.BeginTransactionAsync(cancellationToken);
            CombatConsumableUse? use = await dbContext.CombatConsumableUses
                .SingleOrDefaultAsync(
                    candidate => candidate.SessionId == sessionId
                        && candidate.CommandId == commandId,
                    cancellationToken);
            if (use is not null)
            {
                await RefundUseAsync(use, cancellationToken);
                dbContext.CombatConsumableUses.Remove(use);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        });
    }

    public async Task<int> RecoverInterruptedAsync(
        CancellationToken cancellationToken)
    {
        ActiveCombatSession[] sessions = await dbContext.ActiveCombatSessions
            .OrderBy(state => state.StartedAtUtc)
            .ToArrayAsync(cancellationToken);
        if (sessions.Length == 0)
            return 0;

        foreach (ActiveCombatSession session in sessions)
        {
            bool rewardCommitted = await dbContext.CombatRewardGrants
                .AsNoTracking()
                .AnyAsync(
                    grant => grant.CombatSessionId == session.SessionId,
                    cancellationToken);
            if (!rewardCommitted)
            {
                CombatConsumableUse[] uses = await dbContext.CombatConsumableUses
                    .Where(use => use.SessionId == session.SessionId)
                    .OrderBy(use => use.UsedAtUtc)
                    .ToArrayAsync(cancellationToken);
                foreach (CombatConsumableUse use in uses)
                    await RefundUseAsync(use, cancellationToken);
            }

            dbContext.ActiveCombatSessions.Remove(session);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogWarning(
                "Recovered interrupted combat session {SessionId} for character {CharacterId}; "
                + "rewardCommitted={RewardCommitted}.",
                session.SessionId,
                session.CharacterId,
                rewardCommitted);
        }

        return sessions.Length;
    }

    private async Task RefundUseAsync(
        CombatConsumableUse use,
        CancellationToken cancellationToken)
    {
        CharacterItem? stack = await dbContext.CharacterItems
            .Where(item =>
                item.CharacterId == use.CharacterId
                && item.ItemDefinitionId == use.ItemDefinitionId
                && item.DefinitionVersion == use.DefinitionVersion
                && item.Quantity < use.MaxStack)
            .OrderBy(item => item.AcquiredAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (stack is not null)
        {
            stack.AddQuantity(1, use.MaxStack);
            return;
        }

        dbContext.CharacterItems.Add(new CharacterItem(
            Guid.NewGuid(),
            use.CharacterId,
            use.ItemDefinitionId,
            1,
            use.UsedAtUtc,
            use.DefinitionVersion));
    }
}
