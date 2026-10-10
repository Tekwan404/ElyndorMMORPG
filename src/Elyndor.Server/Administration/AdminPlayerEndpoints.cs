using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Content;
using Elyndor.Core.Content;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Server.Administration;

/// <summary>Read-only administration lookup; never exposes account credentials or auth tokens.</summary>
public static class AdminPlayerEndpoints
{
    public static IEndpointRouteBuilder MapAdminPlayerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/admin/players", ListAsync)
            .WithTags("Admin Players")
            .RequireAuthorization(AdminAuthorization.PolicyName);
        endpoints.MapGet("/api/v1/admin/players/{telegramUserId:long}/items", GetItemsAsync)
            .WithTags("Admin Players")
            .RequireAuthorization(AdminAuthorization.PolicyName);
        endpoints.MapGet("/api/v1/admin/players/{telegramUserId:long}", GetByTelegramIdAsync)
            .WithTags("Admin Players")
            .RequireAuthorization(AdminAuthorization.PolicyName);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        int? page,
        string? search,
        GameDbContext db,
        CancellationToken cancellationToken)
    {
        int currentPage = Math.Clamp(page ?? 1, 1, 10000);
        string query = (search ?? string.Empty).Trim();
        if (query.Length > 64) return Results.BadRequest(new { code = "admin_player_search_too_long" });

        IQueryable<Elyndor.Core.Identity.Account> accounts = db.Accounts.AsNoTracking();
        if (query.Length > 0)
        {
            long telegramId = long.TryParse(query, out long parsedId) ? parsedId : -1;
            string pattern = $"%{query}%";
            accounts = accounts.Where(account =>
                (account.TelegramUsername != null && EF.Functions.ILike(account.TelegramUsername, pattern))
                || account.TelegramUserId == telegramId
                || db.Characters.Any(character =>
                    character.AccountId == account.Id && EF.Functions.ILike(character.Name, pattern)));
        }

        int total = await accounts.CountAsync(cancellationToken);
        var rows = await (
            from account in accounts
            join character in db.Characters.AsNoTracking()
                on account.Id equals character.AccountId into characters
            from character in characters.DefaultIfEmpty()
            orderby account.LastSeenAtUtc descending, account.Id
            select new
            {
                account.TelegramUserId,
                account.TelegramUsername,
                account.LastSeenAtUtc,
                Character = character == null ? null : new
                {
                    character.Name,
                    character.Level,
                    character.ClassId
                }
            })
            .Skip((currentPage - 1) * 50)
            .Take(50)
            .ToArrayAsync(cancellationToken);

        return Results.Ok(new { total, page = currentPage, pageSize = 50, players = rows });
    }

    private static async Task<IResult> GetItemsAsync(
        long telegramUserId,
        GameDbContext db,
        IContentSnapshotProvider contentProvider,
        CancellationToken cancellationToken)
    {
        if (telegramUserId <= 0)
            return Results.BadRequest(new { code = "admin_player_invalid_id" });
        var owner = await (
            from account in db.Accounts.AsNoTracking()
            join character in db.Characters.AsNoTracking() on account.Id equals character.AccountId
            where account.TelegramUserId == telegramUserId
            select new { character.Id, character.Name })
            .SingleOrDefaultAsync(cancellationToken);
        if (owner is null) return Results.NotFound(new { code = "admin_player_not_found" });

        var items = await db.CharacterItems.AsNoTracking()
            .Where(item => item.CharacterId == owner.Id)
            .OrderByDescending(item => item.AcquiredAtUtc)
            .Select(item => new
            {
                item.Id,
                item.ItemDefinitionId,
                item.Quantity,
                item.Stars,
                item.EnhancementLevel,
                item.SourceType,
                item.IsLocked,
                item.ItemLevel,
                item.GenerationVersion
            })
            .Take(300)
            .ToArrayAsync(cancellationToken);
        var equippedIds = await db.CharacterEquipment.AsNoTracking()
            .Where(equipment => equipment.CharacterId == owner.Id)
            .Select(equipment => equipment.CharacterItemId)
            .ToArrayAsync(cancellationToken);
        HashSet<Guid> equipped = [.. equippedIds];
        var definitions = contentProvider.GetCurrent().Indexes.ItemsById;
        var results = items.Select(item => new
        {
            item.Id,
            item.ItemDefinitionId,
            Name = definitions.TryGetValue(item.ItemDefinitionId, out var definition)
                ? definition.Name : item.ItemDefinitionId,
            item.Quantity,
            item.Stars,
            item.EnhancementLevel,
            item.SourceType,
            item.IsLocked,
            item.ItemLevel,
            CanClone = item.GenerationVersion > 0,
            IsEquipped = equipped.Contains(item.Id)
        });
        return Results.Ok(new { owner.Name, items = results });
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
