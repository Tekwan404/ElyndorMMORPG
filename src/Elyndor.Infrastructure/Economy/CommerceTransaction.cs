using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Economy;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.Economy;

public sealed class CommerceTransaction(GameDbContext db, IContentSnapshotProvider content, TimeProvider time)
{
    public static Guid OperationId(Guid account, Guid request) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"{account:N}:{request:N}")).AsSpan(0, 16));
    public async Task<CommerceResult<T>> RunAsync<T>(Guid accountId, Guid requestId, string operation,
        object payload, Guid[] participants, Func<Character[], bool, Task<T>> action, CancellationToken ct)
    {
        if (requestId == Guid.Empty) return CommerceResult.Failure<T>("commerce_invalid_request");
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload))));
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var characters = await db.Characters.FromSqlInterpolated(
                    $"SELECT * FROM game.characters WHERE \"Id\" = ANY({participants}) ORDER BY \"Id\" FOR UPDATE")
                    .ToArrayAsync(ct);
                var actor = characters.SingleOrDefault(x => x.AccountId == accountId)
                    ?? throw new CommerceRuleException("commerce_character_not_found");
                if (characters.Length != participants.Distinct().Count()) throw new CommerceRuleException("commerce_character_not_found");
                var receipt = await db.CharacterMutations.FindAsync([actor.Id, requestId], ct);
                if (receipt is not null && (receipt.OperationType != operation || receipt.RequestFingerprint != hash))
                    throw new CommerceRuleException("commerce_request_conflict");
                T result = await action(characters, receipt is not null);
                if (receipt is null) db.CharacterMutations.Add(new CharacterMutation(actor.Id, requestId, operation, hash, time.GetUtcNow()));
                await db.SaveChangesAsync(ct);
                ct.ThrowIfCancellationRequested();
                await transaction.CommitAsync(ct);
                return CommerceResult.Success<T>(result);
            }
            catch (CommerceRuleException ex)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                return CommerceResult.Failure<T>(ex.Code);
            }
            catch (OverflowException)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                return CommerceResult.Failure<T>("commerce_balance_limit");
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                return CommerceResult.Failure<T>("commerce_conflict");
            }
        });
    }

    public Task<Guid?> CharacterIdAsync(Guid account, CancellationToken ct) => db.Characters.AsNoTracking()
        .Where(x => x.AccountId == account).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);

    public async Task<CharacterItem> ItemAsync(Guid itemId, Guid owner, Guid? operation, bool auction, CancellationToken ct,
        bool verifyTradePolicy = true)
    {
        var item = await db.CharacterItems.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == itemId, ct)
            ?? throw new CommerceRuleException("commerce_item_missing");
        if (item.CharacterId != owner) throw new CommerceRuleException("commerce_item_not_owned");
        if (verifyTradePolicy)
        {
            var definition = content.GetCurrent().Package.Items?.SingleOrDefault(x => x.Id == item.ItemDefinitionId);
            if (definition is null || definition.TradePolicyId is not (null or "ORDINARY_TRADEABLE")
                || item.BindState != ItemBindStates.Unbound || item.IsLocked)
                throw new CommerceRuleException("commerce_item_not_tradeable");
        }
        if (item.TransactionLockId != operation || (auction ? item.Storage != "AUCTION" : item.Storage != "INVENTORY"))
            throw new CommerceRuleException("commerce_item_locked");
        if (await db.CharacterEquipment.AnyAsync(x => x.CharacterItemId == itemId, ct)
            || await db.CharacterSpatialArtifacts.AnyAsync(x => x.CharacterItemId == itemId, ct))
            throw new CommerceRuleException("commerce_item_equipped");
        return item;
    }

    public async Task EligibleAsync(Guid[] ids, CancellationToken ct)
    {
        if (await db.ActiveCombatSessions.AnyAsync(x => ids.Contains(x.CharacterId), ct)
            || await db.CharacterVitals.AnyAsync(x => ids.Contains(x.CharacterId) && x.CurrentHp <= 0, ct)
            || await db.CharacterTravelStates.AnyAsync(x => ids.Contains(x.CharacterId), ct))
            throw new CommerceRuleException("commerce_character_unavailable");
        var locations = await db.CharacterLocations.Where(x => ids.Contains(x.CharacterId)).Select(x => x.LocationId).ToArrayAsync(ct);
        if (locations.Length != ids.Length || locations.Distinct().Count() != 1)
            throw new CommerceRuleException("trade_location_mismatch");
    }
}
