using Elyndor.Core.Administration;
using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Core.Talents;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Content;
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
    Message
}

public sealed record AdministrationOperation(
    AdministrationOperationType Type,
    long TargetTelegramUserId,
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
    ITelegramMessageSender? messageSender = null)
{
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

        AdministrationResult result = await ExecuteCharacterOperationAsync(
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
        AdministrationOperation operation,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        Account? account = await dbContext.Accounts.SingleOrDefaultAsync(
            candidate => candidate.TelegramUserId == operation.TargetTelegramUserId,
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
            if (messageSender is null)
            {
                throw new InvalidOperationException("Telegram sender is not configured.");
            }

            await messageSender.SendAsync(operation.TargetTelegramUserId, operation.Value!, cancellationToken);
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
            && !audit.ResultCode.Contains("taken", StringComparison.Ordinal),
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
