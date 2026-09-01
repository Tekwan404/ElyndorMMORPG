using Elyndor.Core.Administration;
using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Persistence;
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
    GameContentPackage content,
    ITelegramMessageSender? messageSender = null)
{
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
                return SetLevel(character, vitals, operation.NumericValue!.Value, now);
            case AdministrationOperationType.Restore:
                return Restore(character, vitals, now);
            case AdministrationOperationType.SetLocation:
                if (!content.Locations.Any(candidate => candidate.Id == operation.Value))
                {
                    return Failure("admin_location_invalid", "Локация отсутствует в content package.");
                }

                location.Relocate(operation.Value!, now);
                return Success("admin_location_updated", $"{character.Name}: локация → {operation.Value}.");
            case AdministrationOperationType.Rename:
                return Rename(character, operation.Value!);
            case AdministrationOperationType.SetClass:
                return SetClass(character, vitals, operation.Value!, now);
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

    private AdministrationResult SetLevel(
        Character character,
        CharacterVitals vitals,
        int level,
        DateTimeOffset now)
    {
        CharacterStats oldStats = StatCalculator().Calculate(character.ClassId, character.Level);
        decimal oldResourceMax = GetResourceMax(character.ClassId);
        character.SetLevel(level);
        CharacterStats newStats = StatCalculator().Calculate(character.ClassId, character.Level);
        decimal newResourceMax = GetResourceMax(character.ClassId);
        vitals.Checkpoint(
            Scale(vitals.CurrentHp, oldStats.MaxHp, newStats.MaxHp),
            Scale(vitals.CurrentResource, oldResourceMax, newResourceMax),
            now);
        return Success("admin_level_updated", $"{character.Name}: уровень → {level}.");
    }

    private AdministrationResult Restore(Character character, CharacterVitals vitals, DateTimeOffset now)
    {
        CharacterStats stats = StatCalculator().Calculate(character.ClassId, character.Level);
        vitals.Checkpoint(stats.MaxHp, GetResourceMax(character.ClassId), now);
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

    private AdministrationResult SetClass(
        Character character,
        CharacterVitals vitals,
        string classId,
        DateTimeOffset now)
    {
        if (!HasDefinition("CLASS", classId)
            || content.ClassProfiles?.Any(profile => profile.Id == classId) != true)
        {
            return Failure("admin_class_invalid", "Класс отсутствует в content package.");
        }

        CharacterStats oldStats = StatCalculator().Calculate(character.ClassId, character.Level);
        decimal oldResourceMax = GetResourceMax(character.ClassId);
        character.ChangeClass(classId);
        CharacterStats newStats = StatCalculator().Calculate(character.ClassId, character.Level);
        vitals.Checkpoint(
            Scale(vitals.CurrentHp, oldStats.MaxHp, newStats.MaxHp),
            Scale(vitals.CurrentResource, oldResourceMax, GetResourceMax(character.ClassId)),
            now);
        return Success("admin_class_updated", $"{character.Name}: класс → {classId}.");
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

    private CharacterStatCalculator StatCalculator() => new(content.StatFormula!, content.ClassProfiles!);

    private decimal GetResourceMax(string classId)
    {
        ClassProfile profile = content.ClassProfiles!.Single(candidate => candidate.Id == classId);
        return content.ResourceProfiles!.Single(candidate => candidate.Id == profile.ResourceProfileId).MaxValue;
    }

    private bool HasDefinition(string type, string id) => content.Definitions.Any(
        definition => definition.Type == type && definition.Id == id);

    private static decimal Scale(decimal current, decimal oldMax, decimal newMax) =>
        oldMax <= 0
            ? decimal.Min(current, newMax)
            : decimal.Clamp(decimal.Round(current / oldMax * newMax, 3, MidpointRounding.AwayFromZero), 0, newMax);

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
