using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.Items;

public static class InventoryCapacity
{
    public const int DefaultCapacity = 40;

    public static int Resolve(GameContentSnapshot contentSnapshot) =>
        contentSnapshot.Package.InventoryProfile?.DefaultCapacity
        ?? DefaultCapacity;

    public static Task<int> CountUsedSlotsAsync(
        GameDbContext dbContext,
        Guid characterId,
        CancellationToken cancellationToken) =>
        dbContext.CharacterItems
            .AsNoTracking()
            .Where(item => item.CharacterId == characterId)
            .Where(item => !dbContext.CharacterEquipment
                .Any(equipment =>
                    equipment.CharacterId == characterId
                    && equipment.CharacterItemId == item.Id))
            .CountAsync(cancellationToken);

    public static async Task<bool> CanAddAsync(
        GameDbContext dbContext,
        Guid characterId,
        ItemDefinition definition,
        int quantity,
        GameContentSnapshot contentSnapshot,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        int capacity = Resolve(contentSnapshot);
        int used = await CountUsedSlotsAsync(
            dbContext,
            characterId,
            cancellationToken);
        int required = await AdditionalSlotsRequiredAsync(
            dbContext,
            characterId,
            definition,
            quantity,
            cancellationToken);
        return used + required <= capacity;
    }

    public static async Task<int> FreeSlotsAsync(
        GameDbContext dbContext,
        Guid characterId,
        GameContentSnapshot contentSnapshot,
        CancellationToken cancellationToken) =>
        Math.Max(
            0,
            Resolve(contentSnapshot)
            - await CountUsedSlotsAsync(
                dbContext,
                characterId,
                cancellationToken));

    public static async Task<int> AdditionalSlotsRequiredAsync(
        GameDbContext dbContext,
        Guid characterId,
        ItemDefinition definition,
        int quantity,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        if (!definition.Stackable)
            return quantity;

        int freeUnits = await dbContext.CharacterItems
            .AsNoTracking()
            .Where(item =>
                item.CharacterId == characterId
                && item.ItemDefinitionId == definition.Id
                && item.DefinitionVersion == definition.Version
                && item.Quantity < definition.MaxStack)
            .SumAsync(
                item => definition.MaxStack - item.Quantity,
                cancellationToken);
        int remaining = Math.Max(0, quantity - freeUnits);
        return remaining == 0
            ? 0
            : (remaining + definition.MaxStack - 1) / definition.MaxStack;
    }
}
