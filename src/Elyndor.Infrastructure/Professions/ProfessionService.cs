using System.Buffers.Binary;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using Elyndor.Core.Characters;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.Professions;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Elyndor.Infrastructure.Professions;

public static class ProfessionErrorCodes
{
    public const string CharacterNotFound = "character_not_found";
    public const string ProfessionNotFound = "profession_not_found";
    public const string ProfessionAlreadyLearned = "profession_already_learned";
    public const string ProfessionLimitReached = "profession_limit_reached";
    public const string ProfessionNotLearned = "profession_not_learned";
    public const string CorpseNotFound = "skinning_corpse_not_found";
    public const string CorpseExpired = "skinning_corpse_expired";
    public const string CorpseAlreadySkinned = "skinning_corpse_already_skinned";
    public const string SkillTooLow = "profession_skill_too_low";
    public const string RecipeNotFound = "profession_recipe_not_found";
    public const string WrongWorkshop = "profession_wrong_workshop";
    public const string MissingIngredients = "profession_missing_ingredients";
    public const string InventoryFull = "inventory_full";
    public const string IdempotencyConflict = "profession_idempotency_conflict";
}

public sealed record ProfessionStateItem(string Id, string Name, ProfessionCategory Category, int Skill, int MaxSkill);
public sealed record SkinnableCorpseState(Guid CombatSessionId, Guid EnemyActorId, string MonsterDefinitionId, string MonsterName, int RequiredSkill, DateTimeOffset ExpiresAtUtc);
public sealed record ProfessionRecipeState(string Id, string Name, string ProfessionId, int RequiredSkill, string OutputItemId, int OutputQuantity, IReadOnlyList<ProfessionRecipeIngredient> Ingredients, string? RequiredLocationId);
public sealed record ProfessionStateSnapshot(IReadOnlyList<ProfessionStateItem> Learned, IReadOnlyList<SkinnableCorpseState> SkinnableCorpses, IReadOnlyList<ProfessionRecipeState> Recipes);
public sealed record ProfessionMutationResult(bool IsSuccess, string? ErrorCode, ProfessionStateSnapshot? State = null, string? ItemId = null, int Quantity = 0, bool SkillIncreased = false, bool Replayed = false);

public sealed class ProfessionService(
    GameDbContext dbContext,
    IContentSnapshotProvider contentProvider,
    TimeProvider timeProvider)
{
    private const int MaxPrimaryProfessions = 2;
    private const int MaxProfessionSkill = 300;
    private static readonly TimeSpan CorpseLifetime = TimeSpan.FromMinutes(30);

    public async Task<ProfessionStateSnapshot?> GetStateAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var character = await dbContext.Characters.AsNoTracking()
            .SingleOrDefaultAsync(item => item.AccountId == accountId, cancellationToken);
        if (character is null)
            return null;

        GameContentPackage package = contentProvider.GetCurrent().Package;
        IReadOnlyList<ProfessionDefinition> definitions = package.Professions ?? [];
        Dictionary<string, ProfessionDefinition> definitionById = definitions.ToDictionary(item => item.Id, StringComparer.Ordinal);
        CharacterProfession[] learned = await dbContext.CharacterProfessions.AsNoTracking()
            .Where(item => item.CharacterId == character.Id)
            .OrderBy(item => item.ProfessionId)
            .ToArrayAsync(cancellationToken);

        Dictionary<string, SkinningSourceDefinition> skinningByMonster = (package.SkinningSources ?? [])
            .GroupBy(item => item.MonsterId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        Dictionary<string, string> monsterNames = (package.Monsters ?? [])
            .ToDictionary(item => item.Id, item => item.Name, StringComparer.Ordinal);
        HashSet<string> learnedIds = learned.Select(item => item.ProfessionId).ToHashSet(StringComparer.Ordinal);
        SkinnableCorpse[] corpses = learnedIds.Contains(ProfessionIds.Skinning)
            ? await dbContext.SkinnableCorpses.AsNoTracking()
                .Where(item => item.CharacterId == character.Id
                    && item.SkinnedAtUtc == null
                    && item.ExpiresAtUtc > timeProvider.GetUtcNow())
                .OrderBy(item => item.ExpiresAtUtc)
                .ToArrayAsync(cancellationToken)
            : [];

        return new ProfessionStateSnapshot(
            learned.Select(item =>
            {
                ProfessionDefinition? definition = definitionById.GetValueOrDefault(item.ProfessionId);
                return new ProfessionStateItem(
                    item.ProfessionId,
                    definition?.Name ?? item.ProfessionId,
                    definition?.Category ?? ProfessionCategory.Gathering,
                    item.Skill,
                    definition?.MaxSkill ?? MaxProfessionSkill);
            }).ToArray(),
            corpses.Where(item => skinningByMonster.ContainsKey(item.MonsterDefinitionId))
                .Select(item =>
                {
                    SkinningSourceDefinition source = skinningByMonster[item.MonsterDefinitionId];
                    return new SkinnableCorpseState(
                        item.CombatSessionId,
                        item.EnemyActorId,
                        item.MonsterDefinitionId,
                        monsterNames.GetValueOrDefault(item.MonsterDefinitionId) ?? item.MonsterDefinitionId,
                        source.RequiredSkill,
                        item.ExpiresAtUtc);
                }).ToArray(),
            (package.ProfessionRecipes ?? [])
                .Where(recipe => learnedIds.Contains(recipe.ProfessionId))
                .OrderBy(recipe => recipe.RequiredSkill)
                .ThenBy(recipe => recipe.Id, StringComparer.Ordinal)
                .Select(recipe => new ProfessionRecipeState(
                    recipe.Id,
                    recipe.Name,
                    recipe.ProfessionId,
                    recipe.RequiredSkill,
                    recipe.OutputItemId,
                    recipe.OutputQuantity,
                    recipe.Ingredients,
                    recipe.RequiredLocationId))
                .ToArray());
    }

    public async Task<ProfessionMutationResult> LearnAsync(
        Guid accountId,
        string professionId,
        Guid mutationId,
        CancellationToken cancellationToken)
    {
        if (mutationId == Guid.Empty || string.IsNullOrWhiteSpace(professionId))
            return new ProfessionMutationResult(false, ProfessionErrorCodes.ProfessionNotFound);

        var character = await dbContext.Characters.SingleOrDefaultAsync(item => item.AccountId == accountId, cancellationToken);
        if (character is null)
            return new ProfessionMutationResult(false, ProfessionErrorCodes.CharacterNotFound);

        GameContentPackage package = contentProvider.GetCurrent().Package;
        ProfessionDefinition? definition = (package.Professions ?? []).SingleOrDefault(item => item.Id == professionId);
        if (definition is null)
            return new ProfessionMutationResult(false, ProfessionErrorCodes.ProfessionNotFound);

        string fingerprint = Fingerprint($"LEARN|{professionId}");
        CharacterMutation? previous = await dbContext.CharacterMutations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.CharacterId == character.Id && item.MutationId == mutationId, cancellationToken);
        if (previous is not null)
        {
            if (!string.Equals(previous.OperationType, "PROFESSION_LEARN", StringComparison.Ordinal)
                || !string.Equals(previous.RequestFingerprint, fingerprint, StringComparison.Ordinal))
                return new ProfessionMutationResult(false, ProfessionErrorCodes.IdempotencyConflict);
            return new ProfessionMutationResult(true, null, await GetStateAsync(accountId, cancellationToken), Replayed: true);
        }

        if (await dbContext.CharacterProfessions.AnyAsync(item => item.CharacterId == character.Id && item.ProfessionId == professionId, cancellationToken))
            return new ProfessionMutationResult(false, ProfessionErrorCodes.ProfessionAlreadyLearned);
        if (await dbContext.CharacterProfessions.CountAsync(item => item.CharacterId == character.Id, cancellationToken) >= MaxPrimaryProfessions)
            return new ProfessionMutationResult(false, ProfessionErrorCodes.ProfessionLimitReached);

        DateTimeOffset now = timeProvider.GetUtcNow();
        dbContext.CharacterProfessions.Add(new CharacterProfession(character.Id, professionId, now));
        dbContext.CharacterMutations.Add(new CharacterMutation(character.Id, mutationId, "PROFESSION_LEARN", fingerprint, now));
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ProfessionMutationResult(true, null, await GetStateAsync(accountId, cancellationToken));
    }

    public async Task<ProfessionMutationResult> SkinAsync(
        Guid accountId,
        Guid combatSessionId,
        Guid enemyActorId,
        Guid mutationId,
        CancellationToken cancellationToken)
    {
        if (combatSessionId == Guid.Empty || enemyActorId == Guid.Empty || mutationId == Guid.Empty)
            return new ProfessionMutationResult(false, ProfessionErrorCodes.CorpseNotFound);

        return await dbContext.Database.CreateExecutionStrategy().ExecuteInTransactionAsync(
            async _ =>
        {
            dbContext.ChangeTracker.Clear();
            var character = await dbContext.Characters.SingleOrDefaultAsync(item => item.AccountId == accountId, cancellationToken);
            if (character is null)
                return new ProfessionMutationResult(false, ProfessionErrorCodes.CharacterNotFound);

            CharacterProfession? profession = await dbContext.CharacterProfessions
                .SingleOrDefaultAsync(item => item.CharacterId == character.Id && item.ProfessionId == ProfessionIds.Skinning, cancellationToken);
            if (profession is null)
                return new ProfessionMutationResult(false, ProfessionErrorCodes.ProfessionNotLearned);

            SkinnableCorpse? corpse = await dbContext.SkinnableCorpses
                .SingleOrDefaultAsync(item => item.CharacterId == character.Id
                    && item.CombatSessionId == combatSessionId
                    && item.EnemyActorId == enemyActorId, cancellationToken);
            if (corpse is null)
                return new ProfessionMutationResult(false, ProfessionErrorCodes.CorpseNotFound);

            if (corpse.SkinnedAtUtc.HasValue)
            {
                if (corpse.SkinningMutationId == mutationId)
                    return new ProfessionMutationResult(true, null, ItemId: corpse.YieldItemId, Quantity: corpse.YieldQuantity ?? 0, SkillIncreased: corpse.SkillIncreased, Replayed: true);
                return new ProfessionMutationResult(false, ProfessionErrorCodes.CorpseAlreadySkinned);
            }

            DateTimeOffset now = timeProvider.GetUtcNow();
            if (corpse.ExpiresAtUtc <= now)
                return new ProfessionMutationResult(false, ProfessionErrorCodes.CorpseExpired);

            GameContentPackage package = contentProvider.GetCurrent().Package;
            SkinningSourceDefinition? source = (package.SkinningSources ?? [])
                .SingleOrDefault(item => item.MonsterId == corpse.MonsterDefinitionId);
            if (source is null)
                return new ProfessionMutationResult(false, ProfessionErrorCodes.CorpseNotFound);
            if (profession.Skill < source.RequiredSkill)
                return new ProfessionMutationResult(false, ProfessionErrorCodes.SkillTooLow);

            ItemDefinition? itemDefinition = (package.Items ?? []).SingleOrDefault(item => item.Id == source.ItemId);
            if (itemDefinition is null)
                throw new InvalidDataException($"Skinning source '{source.Id}' references missing item '{source.ItemId}'.");

            int quantity = DeterministicRange(mutationId, $"SKIN|{combatSessionId}|{enemyActorId}", source.MinQuantity, source.MaxQuantity);
            ProfessionMutationResult? inventoryFailure = await AddStackableAsync(character.Id, itemDefinition, quantity, now, package, cancellationToken);
            if (inventoryFailure is not null)
                return inventoryFailure;

            bool skillIncreased = ShouldIncreaseSkill(mutationId, $"SKIN_SKILL|{source.Id}", profession.Skill, source.RequiredSkill, source.SkillUpUntil)
                && profession.TryIncreaseSkill(MaxProfessionSkill, now);
            corpse.MarkSkinned(mutationId, itemDefinition.Id, quantity, skillIncreased, now);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new ProfessionMutationResult(true, null, ItemId: itemDefinition.Id, Quantity: quantity, SkillIncreased: skillIncreased);
        },
            _ => Task.FromResult(false),
            IsolationLevel.Serializable,
            cancellationToken);
    }

    public async Task<ProfessionMutationResult> CraftAsync(
        Guid accountId,
        string recipeId,
        Guid mutationId,
        CancellationToken cancellationToken)
    {
        if (mutationId == Guid.Empty || string.IsNullOrWhiteSpace(recipeId))
            return new ProfessionMutationResult(false, ProfessionErrorCodes.RecipeNotFound);

        return await dbContext.Database.CreateExecutionStrategy().ExecuteInTransactionAsync(
            async _ =>
        {
            dbContext.ChangeTracker.Clear();
            var character = await dbContext.Characters.SingleOrDefaultAsync(item => item.AccountId == accountId, cancellationToken);
            if (character is null)
                return new ProfessionMutationResult(false, ProfessionErrorCodes.CharacterNotFound);

            GameContentPackage package = contentProvider.GetCurrent().Package;
            ProfessionRecipeDefinition? recipe = (package.ProfessionRecipes ?? []).SingleOrDefault(item => item.Id == recipeId);
            if (recipe is null)
                return new ProfessionMutationResult(false, ProfessionErrorCodes.RecipeNotFound);

            CharacterProfession? profession = await dbContext.CharacterProfessions
                .SingleOrDefaultAsync(item => item.CharacterId == character.Id && item.ProfessionId == recipe.ProfessionId, cancellationToken);
            if (profession is null)
                return new ProfessionMutationResult(false, ProfessionErrorCodes.ProfessionNotLearned);
            if (profession.Skill < recipe.RequiredSkill)
                return new ProfessionMutationResult(false, ProfessionErrorCodes.SkillTooLow);

            string fingerprint = Fingerprint($"CRAFT|{recipeId}");
            CharacterMutation? previous = await dbContext.CharacterMutations.AsNoTracking()
                .SingleOrDefaultAsync(item => item.CharacterId == character.Id && item.MutationId == mutationId, cancellationToken);
            if (previous is not null)
            {
                if (!string.Equals(previous.OperationType, "PROFESSION_CRAFT", StringComparison.Ordinal)
                    || !string.Equals(previous.RequestFingerprint, fingerprint, StringComparison.Ordinal))
                    return new ProfessionMutationResult(false, ProfessionErrorCodes.IdempotencyConflict);
                return new ProfessionMutationResult(true, null, ItemId: recipe.OutputItemId, Quantity: recipe.OutputQuantity, Replayed: true);
            }

            if (!string.IsNullOrWhiteSpace(recipe.RequiredLocationId))
            {
                string? locationId = await dbContext.CharacterLocations.AsNoTracking()
                    .Where(item => item.CharacterId == character.Id)
                    .Select(item => item.LocationId)
                    .SingleOrDefaultAsync(cancellationToken);
                if (!string.Equals(locationId, recipe.RequiredLocationId, StringComparison.Ordinal))
                    return new ProfessionMutationResult(false, ProfessionErrorCodes.WrongWorkshop);
            }

            CharacterItem[] inventory = await dbContext.CharacterItems
                .Where(item => item.CharacterId == character.Id)
                .OrderBy(item => item.AcquiredAtUtc)
                .ToArrayAsync(cancellationToken);
            foreach (ProfessionRecipeIngredient ingredient in recipe.Ingredients)
            {
                int available = inventory.Where(item => item.ItemDefinitionId == ingredient.ItemId && !item.IsLocked && item.TransactionLockId == null).Sum(item => item.Quantity);
                if (available < ingredient.Quantity)
                    return new ProfessionMutationResult(false, ProfessionErrorCodes.MissingIngredients);
            }

            foreach (ProfessionRecipeIngredient ingredient in recipe.Ingredients)
            {
                int remaining = ingredient.Quantity;
                foreach (CharacterItem item in inventory.Where(item => item.ItemDefinitionId == ingredient.ItemId && !item.IsLocked && item.TransactionLockId == null))
                {
                    if (remaining == 0)
                        break;
                    int consume = Math.Min(item.Quantity, remaining);
                    item.RemoveQuantity(consume);
                    remaining -= consume;
                    if (item.Quantity == 0)
                        dbContext.CharacterItems.Remove(item);
                }
            }

            ItemDefinition output = (package.Items ?? []).SingleOrDefault(item => item.Id == recipe.OutputItemId)
                ?? throw new InvalidDataException($"Profession recipe '{recipe.Id}' references missing output '{recipe.OutputItemId}'.");
            if (output.Stackable)
            {
                ProfessionMutationResult? inventoryFailure = await AddStackableAsync(character.Id, output, recipe.OutputQuantity, timeProvider.GetUtcNow(), package, cancellationToken);
                if (inventoryFailure is not null)
                    return inventoryFailure;
            }
            else
            {
                int capacity = package.InventoryProfile?.DefaultCapacity ?? 30;
                int occupiedAfterConsumption = inventory.Count(item => item.Quantity > 0);
                if (occupiedAfterConsumption + recipe.OutputQuantity > capacity)
                    return new ProfessionMutationResult(false, ProfessionErrorCodes.InventoryFull);
                DateTimeOffset now = timeProvider.GetUtcNow();
                for (int ordinal = 0; ordinal < recipe.OutputQuantity; ordinal++)
                {
                    CharacterItem crafted = ItemInstancePersistenceFactory.CreateCharacterItem(
                        character.Id,
                        output,
                        mutationId,
                        "PROFESSION_CRAFT",
                        recipe.Id,
                        ordinal,
                        now,
                        package);
                    dbContext.CharacterItems.Add(crafted);
                }
            }

            DateTimeOffset committedAt = timeProvider.GetUtcNow();
            bool skillIncreased = ShouldIncreaseSkill(mutationId, $"CRAFT_SKILL|{recipe.Id}", profession.Skill, recipe.RequiredSkill, recipe.SkillUpUntil)
                && profession.TryIncreaseSkill(MaxProfessionSkill, committedAt);
            dbContext.CharacterMutations.Add(new CharacterMutation(character.Id, mutationId, "PROFESSION_CRAFT", fingerprint, committedAt));
            await dbContext.SaveChangesAsync(cancellationToken);
            return new ProfessionMutationResult(true, null, ItemId: output.Id, Quantity: recipe.OutputQuantity, SkillIncreased: skillIncreased);
        },
            _ => Task.FromResult(false),
            IsolationLevel.Serializable,
            cancellationToken);
    }

    private async Task<ProfessionMutationResult?> AddStackableAsync(
        Guid characterId,
        ItemDefinition definition,
        int quantity,
        DateTimeOffset now,
        GameContentPackage package,
        CancellationToken cancellationToken)
    {
        int remaining = quantity;
        CharacterItem[] stacks = await dbContext.CharacterItems
            .Where(item => item.CharacterId == characterId && item.ItemDefinitionId == definition.Id && !item.IsLocked && item.TransactionLockId == null)
            .OrderBy(item => item.AcquiredAtUtc)
            .ToArrayAsync(cancellationToken);
        foreach (CharacterItem stack in stacks)
        {
            int room = Math.Max(0, definition.MaxStack - stack.Quantity);
            if (room == 0)
                continue;
            int add = Math.Min(room, remaining);
            stack.AddQuantity(add, definition.MaxStack);
            remaining -= add;
            if (remaining == 0)
                return null;
        }

        int capacity = package.InventoryProfile?.DefaultCapacity ?? 30;
        int occupied = await dbContext.CharacterItems.CountAsync(item => item.CharacterId == characterId, cancellationToken);
        int stacksNeeded = (int)Math.Ceiling(remaining / (double)definition.MaxStack);
        if (occupied + stacksNeeded > capacity)
            return new ProfessionMutationResult(false, ProfessionErrorCodes.InventoryFull);

        while (remaining > 0)
        {
            int stackQuantity = Math.Min(remaining, definition.MaxStack);
            dbContext.CharacterItems.Add(new CharacterItem(Guid.CreateVersion7(), characterId, definition.Id, stackQuantity, now, definition.Version));
            remaining -= stackQuantity;
        }
        return null;
    }

    private static bool ShouldIncreaseSkill(Guid mutationId, string salt, int currentSkill, int requiredSkill, int skillUpUntil)
    {
        if (currentSkill >= skillUpUntil)
            return false;
        if (currentSkill <= requiredSkill)
            return true;
        int range = Math.Max(1, skillUpUntil - requiredSkill);
        decimal chance = (decimal)(skillUpUntil - currentSkill) / range;
        uint roll = DeterministicUInt32(mutationId, salt);
        decimal normalized = roll / (decimal)uint.MaxValue;
        return normalized < chance;
    }

    private static int DeterministicRange(Guid mutationId, string salt, int min, int max)
    {
        if (min < 1 || max < min)
            throw new InvalidDataException("Profession yield range is invalid.");
        uint value = DeterministicUInt32(mutationId, salt);
        return min + (int)(value % (uint)(max - min + 1));
    }

    private static uint DeterministicUInt32(Guid mutationId, string salt)
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes($"{mutationId:N}|{salt}"));
        return BinaryPrimitives.ReadUInt32LittleEndian(digest);
    }

    private static string Fingerprint(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public sealed class ProfessionCorpseService(
    GameDbContext dbContext,
    IContentSnapshotProvider contentProvider)
{
    public async Task RecordEligibleCorpsesAsync(
        Guid characterId,
        CombatSessionSnapshot snapshot,
        GameContentSnapshot? contentSnapshot,
        CancellationToken cancellationToken)
    {
        if (snapshot.Status != CombatSessionStatus.Victory)
            return;
        bool hasSkinning = await dbContext.CharacterProfessions.AsNoTracking()
            .AnyAsync(item => item.CharacterId == characterId && item.ProfessionId == ProfessionIds.Skinning, cancellationToken);
        if (!hasSkinning)
            return;

        GameContentPackage package = (contentSnapshot ?? contentProvider.GetCurrent()).Package;
        Dictionary<string, SkinningSourceDefinition> sources = (package.SkinningSources ?? [])
            .GroupBy(item => item.MonsterId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        if (sources.Count == 0)
            return;

        DateTimeOffset createdAt = snapshot.ServerTimeUtc;
        DateTimeOffset expiresAt = createdAt.Add(CorpseLifetimeForPersistence);
        IEnumerable<CombatActorSnapshot> enemies = snapshot.Enemies ?? [snapshot.Enemy];
        foreach (CombatActorSnapshot enemy in enemies)
        {
            if (!enemy.RewardEligible
                || enemy.Hp > 0
                || !sources.ContainsKey(enemy.DefinitionId))
                continue;
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO game.character_skinnable_corpses
                    (character_id, combat_session_id, enemy_actor_id, monster_definition_id, created_at_utc, expires_at_utc, skill_increased)
                VALUES
                    ({characterId}, {snapshot.SessionId}, {enemy.ActorId}, {enemy.DefinitionId}, {createdAt}, {expiresAt}, FALSE)
                ON CONFLICT (character_id, combat_session_id, enemy_actor_id) DO NOTHING
                """, cancellationToken);
        }
    }

    private static readonly TimeSpan CorpseLifetimeForPersistence = TimeSpan.FromMinutes(30);
}
