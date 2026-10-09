using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Server.Administration;

/// <summary>Read-only administration lookup; never exposes account credentials or auth tokens.</summary>
public static class AdminPlayerEndpoints
{
    public static IEndpointRouteBuilder MapAdminPlayerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/admin/players/{telegramUserId:long}", GetByTelegramIdAsync)
            .WithTags("Admin Players")
            .RequireAuthorization(AdminAuthorization.PolicyName);
        return endpoints;
    }

    private static async Task<IResult> GetByTelegramIdAsync(
        long telegramUserId,
        GameDbContext db,
        CancellationToken cancellationToken)
    {
        if (telegramUserId <= 0) return Results.BadRequest(new { code = "admin_player_invalid_id" });

        var account = await db.Accounts.AsNoTracking()
            .Where(candidate => candidate.TelegramUserId == telegramUserId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.TelegramUserId,
                candidate.TelegramUsername,
                candidate.CreatedAtUtc,
                candidate.LastSeenAtUtc
            }).SingleOrDefaultAsync(cancellationToken);
        if (account is null) return Results.NotFound(new { code = "admin_player_not_found" });

        var character = await db.Characters.AsNoTracking()
            .Where(candidate => candidate.AccountId == account.Id)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.Name,
                candidate.ClassId,
                candidate.RaceId,
                candidate.Level,
                candidate.Experience,
                candidate.Gold
            }).SingleOrDefaultAsync(cancellationToken);

        if (character is null)
        {
            return Results.Ok(new
            {
                account.TelegramUserId,
                account.TelegramUsername,
                account.CreatedAtUtc,
                account.LastSeenAtUtc,
                Character = (object?)null
            });
        }

        var vitals = await db.CharacterVitals.AsNoTracking()
            .Where(candidate => candidate.CharacterId == character.Id)
            .Select(candidate => new { candidate.CurrentHp, candidate.CurrentResource })
            .SingleOrDefaultAsync(cancellationToken);
        var location = await db.CharacterLocations.AsNoTracking()
            .Where(candidate => candidate.CharacterId == character.Id)
            .Select(candidate => candidate.LocationId)
            .SingleOrDefaultAsync(cancellationToken);
        int inventoryCount = await db.CharacterItems.AsNoTracking()
            .CountAsync(candidate => candidate.CharacterId == character.Id, cancellationToken);
        int equippedCount = await db.CharacterEquipment.AsNoTracking()
            .CountAsync(candidate => candidate.CharacterId == character.Id, cancellationToken);
        int gmItemsCount = await db.CharacterItems.AsNoTracking()
            .CountAsync(candidate => candidate.CharacterId == character.Id
                && candidate.SourceType == Elyndor.Core.Items.GmItemForge.SourceType, cancellationToken);

        return Results.Ok(new
        {
            account.TelegramUserId,
            account.TelegramUsername,
            account.CreatedAtUtc,
            account.LastSeenAtUtc,
            Character = new
            {
                character.Id,
                character.Name,
                character.ClassId,
                character.RaceId,
                character.Level,
                character.Experience,
                character.Gold,
                LocationId = location,
                vitals?.CurrentHp,
                vitals?.CurrentResource,
                InventoryCount = inventoryCount,
                EquippedCount = equippedCount,
                GmItemsCount = gmItemsCount
            }
        });
    }
}
