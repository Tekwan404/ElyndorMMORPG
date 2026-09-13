using Elyndor.Core.Administration;
using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Economy;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Core.Talents;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Items;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Elyndor.Infrastructure.Administration;

public enum AdministrationOperationType
{
    ShowCharacter,
    SetLevel,
    Restore,
    SetLocation,
    Rename,
    SetClass,
    SetRace,
    Delete,
    Message,
    GiveItem,
    CreatePromoCode
}

public sealed record AdministrationOperation(
    AdministrationOperationType Type,
    long? TargetTelegramUserId = null,
    string? Value = null,
    int? NumericValue = null);

public sealed record AdministrationResult(
    bool IsSuccess,
    string Code,
    string Message,
    bool IsDuplicate = false);

public interface ITelegramMessageSender
{
    Task SendAsync(long chatId, string text, CancellationToken cancellationToken);
}

public sealed class TelegramAdministrationService(
    GameDbContext dbContext,
    TimeProvider timeProvider,
    IContentSnapshotProvider contentProvider,
    CharacterDerivedStateService derivedStateService,
    ContentAdministrationService? contentAdministrationService = null,
    ITelegramMessageSender? messageSender = null)
{
    private static readonly HashSet<string> AdminItemQualityProfiles =
        new(["NORMAL", "ELITE", "BOSS"], StringComparer.Ordinal);

    public TelegramAdministrationService(
        GameDbContext dbContext,
        TimeProvider timeProvider,
        GameContentPackage content,
        CharacterDerivedStateService derivedStateService,
        ITelegramMessageSender? messageSender = null)
        : this(
            dbContext,
            timeProvider,
            new StaticContentSnapshotProvider(content),
            derivedStateService,
            null,
            messageSender)
    {
    }

    public Task<AdministrationResult> ExecuteAsync(
        long updateId,
        long administratorTelegramUserId,
        AdministrationOperation operation,
        CancellationToken cancellationToken)
    {
        IExecutionStrategy executionStrategy = dbContext.Database.CreateExecutionStrategy();
        return executionStrategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            return await ExecuteCoreAsync(
                updateId,
                administratorTelegramUserId,
                operation,
                cancellationToken);
        });
    }

    private async Task<AdministrationResult> ExecuteCoreAsync(
        long updateId,
        long administratorTelegramUserId,
        AdministrationOperation operation,
        CancellationToken cancellationToken)
    {
        AdminCommandAudit? existing = await dbContext.AdminCommandAudits
            .AsNoTracking()
            .SingleOrDefaultAsync(audit => audit.UpdateId == updateId, cancellationToken);
        if (existing is not null)
        {
            return FromAudit(existing, true);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        AdminCommandAudit audit = new(
            updateId,
            administratorTelegramUserId,
            operation.Type.ToString(),
            operation.TargetTelegramUserId,
            now);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.AdminCommandAudits.Add(audit);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsConstraintViolation(
            exception,
            "pk_admin_command_audits"))
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            existing = await dbContext.AdminCommandAudits.AsNoTracking()
                .SingleAsync(candidate => candidate.UpdateId == updateId, cancellationToken);
            return FromAudit(existing, true);
        }

        if (operation.Type == AdministrationOperationType.Message)
        {
            await transaction.CommitAsync(cancellationToken);
            return await DeliverMessageAsync(audit, operation, cancellationToken);
        }

        if (operation.Type == AdministrationOperationType.CreatePromoCode)
        {
            await transaction.CommitAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            AdministrationResult promoResult = await CreatePromoCodeAsync(
                administratorTelegramUserId,
                operation.Value,
                cancellationToken);
            return await CompleteDeferredAuditAsync(updateId, promoResult, cancellationToken);
        }

        AdministrationResult result = await ExecuteCharacterOperationAsync(
            updateId,
            operation,
            now,
            cancellationToken);
        audit.Complete(result.Code, result.Message, now);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateException exception) when (
            operation.Type == AdministrationOperationType.Rename
            && IsConstraintViolation(exception, "uq_characters_normalized_name"))
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return await RecordTerminalFailureAsync(
                audit,
                "admin_name_taken",
                "Имя уже занято.",
                cancellationToken);
        }
    }

    private async Task<AdministrationResult> ExecuteCharacterOperationAsync(
        long updateId,
        AdministrationOperation operation,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!operation.TargetTelegramUserId.HasValue)
        {
            return Failure("admin_target_invalid", "Для команды нужен Telegram ID игрока.");
        }

        long targetTelegramUserId = operation.TargetTelegramUserId.Value;
        Account? account = await dbContext.Accounts.SingleOrDefaultAsync(
            candidate => candidate.TelegramUserId == targetTelegramUserId,
            cancellationToken);
        if (account is null)
        {
            return Failure("admin_account_not_found", "Telegram-аккаунт не найден.");
        }

        Character? character = await dbContext.Characters.SingleOrDefaultAsync(
            candidate => candidate.AccountId == account.Id,
            cancellationToken);
        if (character is null)
        {
            return Failure("admin_character_not_found", "Персонаж не найден.");
        }

        if (operation.Type == AdministrationOperationType.GiveItem)
        {
            return await GiveItemAsync(character, updateId, operation.Value, now, cancellationToken);
        }

        CharacterVitals vitals = await dbContext.CharacterVitals.SingleAsync(
            candidate => candidate.CharacterId == character.Id,
            cancellationToken);
        CharacterLocation location = await dbContext.CharacterLocations.SingleAsync(
            candidate => candidate.CharacterId == character.Id,
            cancellationToken);

        switch (operation.Type)
        {
            case AdministrationOperationType.ShowCharacter:
                return Success(
                    "admin_character_found",
                    $"{character.Name} | {character.RaceId} {character.ClassId} | ур. {character.Level} | "
                    + $"HP {vitals.CurrentHp:0.##} | ресурс {vitals.CurrentResource:0.##} | {location.LocationId}");
            case AdministrationOperationType.SetLevel:
                return await SetLevelAsync(
                    character,
                    vitals,
                    operation.NumericValue!.Value,
                    now,
                    cancellationToken);
            case AdministrationOperationType.Restore:
                return await RestoreAsync(character, vitals, now, cancellationToken);
            case AdministrationOperationType.SetLocation:
                if (!contentProvider.GetCurrent().Indexes.LocationsById.ContainsKey(operation.Value!))
                {
                    return Failure("admin_location_invalid", "Локация отсутствует в content package.");
                }

                location.Relocate(operation.Value!, now);
                return Success("admin_location_updated", $"{character.Name}: локация → {operation.Value}.");
            case AdministrationOperationType.Rename:
                return Rename(character, operation.Value!);
            case AdministrationOperationType.SetClass:
                return await SetClassAsync(
                    character,
                    vitals,
                    operation.Value!,
                    now,
                    cancellationToken);
            case AdministrationOperationType.SetRace:
                if (!HasDefinition("RACE", operation.Value!))
                {
                    return Failure("admin_race_invalid", "Раса отсутствует в content package.");
                }

                character.ChangeRace(operation.Value!);
                return Success("admin_race_updated", $"{character.Name}: раса → {operation.Value}.");
            case AdministrationOperationType.Delete:
                if (!string.Equals(character.Name, operation.Value, StringComparison.Ordinal))
                {
                    return Failure("admin_delete_name_mismatch", "Имя подтверждения не совпадает.");
                }

                dbContext.Characters.Remove(character);
                return Success("admin_character_deleted", $"Персонаж {character.Name} удалён; аккаунт сохранён.");
            default:
                return Failure("admin_operation_invalid", "Команда не поддерживается.");
        }
    }

    private async Task<AdministrationResult> GiveItemAsync(
        Character character,
        long updateId,
        string? rawSpec,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!TryParseItemGrant(rawSpec, out string itemId, out int quantity, out string qualityProfile))
        {
            return Failure(
                "admin_item_invalid",
                "Формат: giveitem <telegramId> <itemId> [quantity] [NORMAL|ELITE|BOSS].");
        }

        GameContentSnapshot content = contentProvider.GetCurrent();
        if (!content.Indexes.ItemsById.TryGetValue(itemId, out ItemDefinition? definition))
        {
            return Failure("admin_item_not_found", $"Предмет {itemId} отсутствует в content package.");
        }

        int usedSlots = await InventoryCapacity.CountUsedSlotsAsync(
            dbContext,
            character.Id,
            cancellationToken);
        int additionalSlots = await InventoryCapacity.AdditionalSlotsRequiredAsync(
            dbContext,
            character.Id,
            definition,
            quantity,
            cancellationToken);
        if (usedSlots + additionalSlots > InventoryCapacity.Resolve(content))
        {
            return Failure("admin_inventory_full", $"{character.Name}: в инвентаре недостаточно места.");
        }

        Guid sourceOperationId = Guid.CreateVersion7();
        if (!definition.Stackable)
        {
            for (var ordinal = 0; ordinal < quantity; ordinal++)
            {
                CharacterItem item = ItemInstancePersistenceFactory.CreateCharacterItem(
                    character.Id,
                    definition,
                    sourceOperationId,
                    "ADMIN_GRANT",
                    $"telegram-update:{updateId}",
                    ordinal,
                    now,
                    content.Package,
                    qualityProfile);
                dbContext.CharacterItems.Add(item);
            }
        }
        else
        {
            CharacterItem[] stacks = await dbContext.CharacterItems
                .Where(item => item.CharacterId == character.Id
                    && item.ItemDefinitionId == definition.Id
                    && item.DefinitionVersion == definition.Version
                    && item.Quantity < definition.MaxStack)
                .OrderBy(item => item.AcquiredAtUtc)
                .ToArrayAsync(cancellationToken);
            int remaining = quantity;
            foreach (CharacterItem stack in stacks)
            {
                int added = Math.Min(definition.MaxStack - stack.Quantity, remaining);
                stack.AddQuantity(added, definition.MaxStack);
                remaining -= added;
                if (remaining == 0) break;
            }

            while (remaining > 0)
            {
                int stackSize = Math.Min(definition.MaxStack, remaining);
                dbContext.CharacterItems.Add(new CharacterItem(
                    Guid.CreateVersion7(),
                    character.Id,
                    definition.Id,
                    stackSize,
                    now,
                    definition.Version));
                remaining -= stackSize;
            }
        }

        return Success(
            "admin_item_granted",
            $"{character.Name}: выдано {definition.Name} ×{quantity} ({definition.Id}), качество {qualityProfile}.");
    }

    private async Task<AdministrationResult> CreatePromoCodeAsync(
        long administratorTelegramUserId,
        string? rawSpec,
        CancellationToken cancellationToken)
    {
        if (contentAdministrationService is null)
        {
            return Failure("admin_promo_unavailable", "Hot content administration недоступен.");
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        GameContentSnapshot content = contentProvider.GetCurrent();
        if (!TryParsePromoCode(
                rawSpec,
                content,
                now,
                out PromoCodeDefinition? promo,
                out string errorMessage))
        {
            return Failure("admin_promo_invalid", errorMessage);
        }

        ContentAdminRuntimeState current = contentAdministrationService.GetCurrent();
        if ((current.Package.PromoCodes ?? []).Any(existing =>
                string.Equals(existing.Code, promo!.Code, StringComparison.Ordinal)))
        {
            return Failure("admin_promo_exists", $"Промокод {promo!.Code} уже существует.");
        }

        GameContentPackage candidate = current.Package with
        {
            PublishedAtUtc = now,
            PromoCodes = [.. (current.Package.PromoCodes ?? []), promo!]
        };
        string payload = GameContentPackageCodec.SerializeCanonical(candidate);
        string actor = $"telegram-admin:{administratorTelegramUserId}";
        string note = $"create promo {promo!.Code}";

        try
        {
            ContentRevision draft = await contentAdministrationService.CreateDraftAsync(
                payload,
                current.PayloadSha256,
                actor,
                note,
                cancellationToken);
            ContentPublicationResult? publication = await contentAdministrationService.PublishAsync(
                draft.Id,
                current.PayloadSha256,
                actor,
                note,
                cancellationToken);
            if (publication is null)
            {
                return Failure("admin_promo_publish_failed", "Не удалось опубликовать промокод.");
            }
        }
        catch (ContentDraftConflictException)
        {
            return Failure("admin_promo_conflict", "Live content изменился. Повтори команду.");
        }
        catch (ContentPublicationConflictException)
        {
            return Failure("admin_promo_conflict", "Live content изменился. Повтори команду.");
        }
        catch (ContentDraftValidationException exception)
        {
            string details = exception.Errors.Count == 0
                ? "Промокод не прошёл валидацию контента."
                : exception.Errors[0].Message;
            return Failure("admin_promo_invalid", details);
        }

        string rewards = DescribePromoRewards(promo!);
        string limits = promo!.GlobalRedemptionLimit is int globalLimit
            ? $"общий лимит {globalLimit}"
            : "без общего лимита";
        string expiry = promo.ExpiresAtUtc is DateTimeOffset expiresAt
            ? $", до {expiresAt:yyyy-MM-dd HH:mm} UTC"
            : string.Empty;
        return Success(
            "admin_promo_created",
            $"Промокод {promo.Code} создан: {rewards}; {limits}, на аккаунт {promo.PerAccountRedemptionLimit ?? 1}{expiry}.");
    }

    private async Task<AdministrationResult> CompleteDeferredAuditAsync(
        long updateId,
        AdministrationResult result,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        AdminCommandAudit audit = await dbContext.AdminCommandAudits
            .SingleAsync(candidate => candidate.UpdateId == updateId, cancellationToken);
        audit.Complete(result.Code, result.Message, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    private static bool TryParseItemGrant(
        string? rawSpec,
        out string itemId,
        out int quantity,
        out string qualityProfile)
    {
        itemId = string.Empty;
        quantity = 1;
        qualityProfile = "NORMAL";
        string[] tokens = rawSpec?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        if (tokens.Length is < 1 or > 3) return false;

        itemId = tokens[0].ToUpperInvariant();
        if (tokens.Length >= 2)
        {
            if (int.TryParse(tokens[1], out int parsedQuantity))
            {
                if (parsedQuantity is < 1 or > 1000) return false;
                quantity = parsedQuantity;
                if (tokens.Length == 3)
                    qualityProfile = tokens[2].ToUpperInvariant();
            }
            else
            {
                if (tokens.Length == 3) return false;
                qualityProfile = tokens[1].ToUpperInvariant();
            }
        }

        return itemId.Length is > 0 and <= 128
            && AdminItemQualityProfiles.Contains(qualityProfile);
    }

    private static bool TryParsePromoCode(
        string? rawSpec,
        GameContentSnapshot content,
        DateTimeOffset now,
        out PromoCodeDefinition? promo,
        out string errorMessage)
    {
        promo = null;
        errorMessage = "Формат: promocode create <CODE> crystals=<amount> [item=<ITEM_ID>:<qty>] [global=<N>] [per=<N>] [hours=<N>].";
        string[] tokens = rawSpec?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        if (tokens.Length < 2) return false;

        string code = NormalizePromoCode(tokens[0]);
        if (code.Length == 0)
        {
            errorMessage = "Код: 1-64 символа A-Z, 0-9 и _, первый символ — буква.";
            return false;
        }

        long crystals = 0;
        int? globalLimit = null;
        int perAccountLimit = 1;
        int? hours = null;
        bool crystalsSeen = false;
        bool globalSeen = false;
        bool perSeen = false;
        bool hoursSeen = false;
        List<PromoItemRewardDefinition> itemRewards = [];

        foreach (string token in tokens.Skip(1))
        {
            int separator = token.IndexOf('=');
            if (separator <= 0 || separator == token.Length - 1) return false;
            string key = token[..separator].ToLowerInvariant();
            string value = token[(separator + 1)..];
            switch (key)
            {
                case "crystals":
                    if (crystalsSeen || !long.TryParse(value, out crystals) || crystals is < 0 or > 1_000_000_000)
                        return false;
                    crystalsSeen = true;
                    break;
                case "global":
                    if (globalSeen || !int.TryParse(value, out int parsedGlobal) || parsedGlobal is < 1 or > 1_000_000)
                        return false;
                    globalLimit = parsedGlobal;
                    globalSeen = true;
                    break;
                case "per":
                    if (perSeen || !int.TryParse(value, out perAccountLimit) || perAccountLimit is < 1 or > 100)
                        return false;
                    perSeen = true;
                    break;
                case "hours":
                    if (hoursSeen || !int.TryParse(value, out int parsedHours) || parsedHours is < 1 or > 8760)
                        return false;
                    hours = parsedHours;
                    hoursSeen = true;
                    break;
                case "item":
                    int quantitySeparator = value.LastIndexOf(':');
                    if (quantitySeparator <= 0
                        || quantitySeparator == value.Length - 1
                        || !int.TryParse(value[(quantitySeparator + 1)..], out int itemQuantity)
                        || itemQuantity is < 1 or > 1000)
                    {
                        return false;
                    }
                    string itemId = value[..quantitySeparator].ToUpperInvariant();
                    if (!content.Indexes.ItemsById.ContainsKey(itemId))
                    {
                        errorMessage = $"Предмет {itemId} отсутствует в content package.";
                        return false;
                    }
                    itemRewards.Add(new PromoItemRewardDefinition(itemId, itemQuantity));
                    break;
                default:
                    return false;
            }
        }

        if (crystals <= 0 && itemRewards.Count == 0)
        {
            errorMessage = "У промокода должна быть награда: crystals>0 и/или item=ITEM_ID:qty.";
            return false;
        }

        promo = new PromoCodeDefinition(
            code,
            crystals,
            itemRewards,
            Enabled: true,
            StartsAtUtc: null,
            ExpiresAtUtc: hours.HasValue ? now.AddHours(hours.Value) : null,
            GlobalRedemptionLimit: globalLimit,
            PerAccountRedemptionLimit: perAccountLimit);
        return true;
    }

    private static string NormalizePromoCode(string raw)
    {
        string code = raw.Trim().ToUpperInvariant();
        if (code.Length is < 1 or > 64
            || code[0] is < 'A' or > 'Z'
            || !code.All(character => char.IsAsciiLetterOrDigit(character) || character == '_'))
        {
            return string.Empty;
        }
        return code;
    }

    private static string DescribePromoRewards(PromoCodeDefinition promo)
    {
        List<string> parts = [];
        if (promo.CrystalAmount > 0) parts.Add($"{promo.CrystalAmount} кристаллов");
        parts.AddRange((promo.ItemRewards ?? []).Select(reward => $"{reward.ItemDefinitionId} ×{reward.Quantity}"));
        return string.Join(", ", parts);
    }

    private async Task<AdministrationResult> SetLevelAsync(
        Character character,
        CharacterVitals vitals,
        int level,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        CharacterDerivedState oldState = await derivedStateService.ResolveAsync(
            character.Id,
            character.ClassId,
            character.Level,
            cancellationToken);

        character.SetLevel(level);
        await NormalizeTalentsForLevelAsync(character, now, cancellationToken);

        // ResolveAsync intentionally reads persisted talent/inventory state using no-tracking
        // queries, so persist the level/talent normalization inside the current admin
        // transaction before resolving the new authoritative state.
        await dbContext.SaveChangesAsync(cancellationToken);

        CharacterDerivedState newState = await derivedStateService.ResolveAsync(
            character.Id,
            character.ClassId,
            character.Level,
            cancellationToken);
        CharacterVitalsScaler.ScaleToDerivedMaximums(
            vitals,
            oldState.Stats.MaxHp,
            newState.Stats.MaxHp,
            oldState.EffectiveResourceProfile.MaxValue,
            newState.EffectiveResourceProfile.MaxValue,
            now);
        return Success("admin_level_updated", $"{character.Name}: уровень → {level}.");
    }

    private async Task<AdministrationResult> RestoreAsync(
        Character character,
        CharacterVitals vitals,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        CharacterDerivedState state = await derivedStateService.ResolveAsync(
            character.Id,
            character.ClassId,
            character.Level,
            cancellationToken);
        vitals.Checkpoint(
            state.Stats.MaxHp,
            state.EffectiveResourceProfile.MaxValue,
            now);
        return Success("admin_character_restored", $"{character.Name}: HP и ресурс восстановлены.");
    }

    private static AdministrationResult Rename(Character character, string value)
    {
        CharacterNameValidationResult name = CharacterNamePolicy.Validate(value);
        if (!name.IsValid)
        {
            return Failure(name.ErrorCode!, "Имя не прошло правила Elyndor.");
        }

        character.Rename(name.DisplayName!, name.NormalizedName!);
        return Success("admin_name_updated", $"Персонаж переименован в {name.DisplayName}.");
    }

    private async Task<AdministrationResult> SetClassAsync(
        Character character,
        CharacterVitals vitals,
        string classId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!HasDefinition("CLASS", classId)
            || !contentProvider.GetCurrent().Indexes.ClassesById.ContainsKey(classId))
        {
            return Failure("admin_class_invalid", "Класс отсутствует в content package.");
        }

        if (string.Equals(character.ClassId, classId, StringComparison.Ordinal))
            return Success("admin_class_updated", $"{character.Name}: класс уже {classId}.");

        CharacterDerivedState oldState = await derivedStateService.ResolveAsync(
            character.Id,
            character.ClassId,
            character.Level,
            cancellationToken);

        character.ChangeClass(classId);

        CharacterEquipment[] equipped = await dbContext.CharacterEquipment
            .Where(candidate => candidate.CharacterId == character.Id)
            .ToArrayAsync(cancellationToken);
        dbContext.CharacterEquipment.RemoveRange(equipped);

        CharacterTalentState? talentState = await dbContext.CharacterTalentStates
            .SingleOrDefaultAsync(
                candidate => candidate.CharacterId == character.Id,
                cancellationToken);
        TalentTreeDefinition? newTree = contentProvider.GetCurrent().Indexes.TalentTreesByClassId
            .GetValueOrDefault(classId);
        if (newTree is null)
        {
            if (talentState is not null)
                dbContext.CharacterTalentStates.Remove(talentState);
        }
        else if (talentState is null)
        {
            dbContext.CharacterTalentStates.Add(new CharacterTalentState(
                character.Id,
                newTree.Id,
                newTree.Version,
                now));
        }
        else
        {
            talentState.Reinitialize(newTree.Id, newTree.Version, now);
        }

        // Persist the new class/equipment/talent shape inside the enclosing admin transaction
        // before resolving the new authoritative state.
        await dbContext.SaveChangesAsync(cancellationToken);

        CharacterDerivedState newState = await derivedStateService.ResolveAsync(
            character.Id,
            character.ClassId,
            character.Level,
            cancellationToken);
        CharacterVitalsScaler.ScaleToDerivedMaximums(
            vitals,
            oldState.Stats.MaxHp,
            newState.Stats.MaxHp,
            oldState.EffectiveResourceProfile.MaxValue,
            newState.EffectiveResourceProfile.MaxValue,
            now);

        return Success(
            "admin_class_updated",
            $"{character.Name}: класс → {classId}; экипировка снята, таланты сброшены.");
    }

    private async Task NormalizeTalentsForLevelAsync(
        Character character,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        TalentTreeDefinition? tree = contentProvider.GetCurrent().Indexes.TalentTreesByClassId
            .GetValueOrDefault(character.ClassId);
        if (tree is null) return;

        CharacterTalentState? state = await dbContext.CharacterTalentStates
            .SingleOrDefaultAsync(
                candidate => candidate.CharacterId == character.Id,
                cancellationToken);
        if (state is null) return;

        if (!string.Equals(state.TalentTreeId, tree.Id, StringComparison.Ordinal))
        {
            state.Reinitialize(tree.Id, tree.Version, now);
            return;
        }

        foreach (string loadoutId in new[] { TalentLoadoutIds.Loadout1, TalentLoadoutIds.Loadout2 })
        {
            if (TalentRules.ValidateBuild(tree, character.Level, state.GetRanks(loadoutId)).Count > 0)
                state.Reset(loadoutId, now);
        }
    }

    private async Task<AdministrationResult> DeliverMessageAsync(
        AdminCommandAudit audit,
        AdministrationOperation operation,
        CancellationToken cancellationToken)
    {
        AdministrationResult result;
        try
        {
            if (messageSender is null || !operation.TargetTelegramUserId.HasValue)
            {
                throw new InvalidOperationException("Telegram sender is not configured or target is missing.");
            }

            await messageSender.SendAsync(operation.TargetTelegramUserId.Value, operation.Value!, cancellationToken);
            result = Success("admin_message_sent", "Сообщение отправлено пользователю.");
        }
        catch
        {
            result = Failure(
                "admin_message_delivery_uncertain",
                "Telegram не подтвердил доставку. Автоповтор отключён, чтобы не отправить дубль.");
        }

        audit.Complete(result.Code, result.Message, timeProvider.GetUtcNow());
        dbContext.Update(audit);
        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    private async Task<AdministrationResult> RecordTerminalFailureAsync(
        AdminCommandAudit original,
        string code,
        string message,
        CancellationToken cancellationToken)
    {
        AdminCommandAudit audit = new(
            original.UpdateId,
            original.AdministratorTelegramUserId,
            original.CommandName,
            original.TargetTelegramUserId,
            original.ReceivedAtUtc);
        audit.Complete(code, message, timeProvider.GetUtcNow());
        dbContext.AdminCommandAudits.Add(audit);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Failure(code, message);
    }

    private bool HasDefinition(string type, string id) =>
        contentProvider.GetCurrent().Indexes.DefinitionsByKey.ContainsKey(
            new GameContentDefinitionKey(type, id));

    private static AdministrationResult Success(string code, string message) => new(true, code, message);

    private static AdministrationResult Failure(string code, string message) => new(false, code, message);

    private static AdministrationResult FromAudit(AdminCommandAudit audit, bool duplicate) =>
        new(audit.ResultCode.StartsWith("admin_", StringComparison.Ordinal)
            && !audit.ResultCode.Contains("invalid", StringComparison.Ordinal)
            && !audit.ResultCode.Contains("not_found", StringComparison.Ordinal)
            && !audit.ResultCode.Contains("mismatch", StringComparison.Ordinal)
            && !audit.ResultCode.Contains("uncertain", StringComparison.Ordinal)
            && !audit.ResultCode.Contains("taken", StringComparison.Ordinal)
            && !audit.ResultCode.Contains("full", StringComparison.Ordinal)
            && !audit.ResultCode.Contains("exists", StringComparison.Ordinal)
            && !audit.ResultCode.Contains("conflict", StringComparison.Ordinal)
            && !audit.ResultCode.Contains("failed", StringComparison.Ordinal)
            && !audit.ResultCode.Contains("unavailable", StringComparison.Ordinal),
            audit.ResultCode,
            audit.ResultSummary,
            duplicate);

    private static bool IsConstraintViolation(DbUpdateException exception, string constraintName) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: var actualConstraint
        }
        && string.Equals(actualConstraint, constraintName, StringComparison.Ordinal);
}
