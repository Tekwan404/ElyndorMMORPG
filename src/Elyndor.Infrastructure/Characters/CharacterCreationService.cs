using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Elyndor.Infrastructure.Characters;

public sealed record CreateCharacterCommand(
    Guid RequestId,
    string Name,
    string RaceId,
    string GenderId,
    string ClassId);

public sealed record CharacterCreationResult(
    bool IsSuccess,
    Character? Character,
    string? ErrorCode)
{
    public static CharacterCreationResult Success(Character character) =>
        new(true, character, null);

    public static CharacterCreationResult Failure(string errorCode) =>
        new(false, null, errorCode);
}

public static class CharacterCreationErrorCodes
{
    public const string InvalidRequest = "character_request_invalid";
    public const string InvalidRoster = "character_roster_invalid";
    public const string IdempotencyConflict = "idempotency_conflict";
    public const string AlreadyExists = "character_already_exists";
    public const string NameTaken = "character_name_taken";
}

public sealed class CharacterCreationService(
    GameDbContext dbContext,
    TimeProvider timeProvider,
    GameContentPackage contentPackage)
{
    private const string AccountConstraint = "uq_characters_account_id";
    private const string CreationRequestConstraint = "uq_characters_creation_request_id";
    private const string NormalizedNameConstraint = "uq_characters_normalized_name";
    private const string InitialLocationId = "STARTER_TOWN";

    private readonly GameDbContext _dbContext =
        dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly GameContentPackage _contentPackage =
        contentPackage ?? throw new ArgumentNullException(nameof(contentPackage));

    public async Task<CharacterCreationResult> CreateAsync(
        Guid accountId,
        CreateCharacterCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (accountId == Guid.Empty || command.RequestId == Guid.Empty)
        {
            return CharacterCreationResult.Failure(
                CharacterCreationErrorCodes.InvalidRequest);
        }

        CharacterNameValidationResult name = CharacterNamePolicy.Validate(command.Name);
        if (!name.IsValid)
        {
            return CharacterCreationResult.Failure(name.ErrorCode!);
        }

        if (!HasDefinition("RACE", command.RaceId)
            || !HasDefinition("GENDER", command.GenderId)
            || !HasDefinition("CLASS", command.ClassId))
        {
            return CharacterCreationResult.Failure(
                CharacterCreationErrorCodes.InvalidRoster);
        }

        IExecutionStrategy executionStrategy = _dbContext.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(
            () => CreateCoreAsync(
                accountId,
                command,
                name,
                _timeProvider.GetUtcNow(),
                cancellationToken));
    }

    public Task<Character?> GetAsync(
        Guid accountId,
        CancellationToken cancellationToken) =>
        _dbContext.Characters
            .AsNoTracking()
            .SingleOrDefaultAsync(
                character => character.AccountId == accountId,
                cancellationToken);

    private async Task<CharacterCreationResult> CreateCoreAsync(
        Guid accountId,
        CreateCharacterCommand command,
        CharacterNameValidationResult name,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        _dbContext.ChangeTracker.Clear();
        await using IDbContextTransaction transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        Character? replay = await _dbContext.Characters
            .AsNoTracking()
            .SingleOrDefaultAsync(
                character => character.CreationRequestId == command.RequestId,
                cancellationToken);
        if (replay is not null)
        {
            CharacterCreationResult replayResult = Matches(
                replay,
                accountId,
                command,
                name)
                    ? CharacterCreationResult.Success(replay)
                    : CharacterCreationResult.Failure(
                        CharacterCreationErrorCodes.IdempotencyConflict);
            await transaction.CommitAsync(cancellationToken);
            return replayResult;
        }

        Character character = new(
            Guid.CreateVersion7(),
            accountId,
            command.RequestId,
            name.DisplayName!,
            name.NormalizedName!,
            command.RaceId,
            command.GenderId,
            command.ClassId,
            now);
        CharacterLocation location = new(character.Id, InitialLocationId, 1, now);
        _dbContext.Characters.Add(character);
        _dbContext.CharacterLocations.Add(location);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return CharacterCreationResult.Success(character);
        }
        catch (DbUpdateException exception) when (IsCreationConflict(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
            return await ResolveConflictAsync(
                accountId,
                command,
                name,
                cancellationToken);
        }
    }

    private async Task<CharacterCreationResult> ResolveConflictAsync(
        Guid accountId,
        CreateCharacterCommand command,
        CharacterNameValidationResult name,
        CancellationToken cancellationToken)
    {
        Character? requestWinner = await _dbContext.Characters
            .AsNoTracking()
            .SingleOrDefaultAsync(
                character => character.CreationRequestId == command.RequestId,
                cancellationToken);
        if (requestWinner is not null)
        {
            return Matches(requestWinner, accountId, command, name)
                ? CharacterCreationResult.Success(requestWinner)
                : CharacterCreationResult.Failure(
                    CharacterCreationErrorCodes.IdempotencyConflict);
        }

        if (await _dbContext.Characters.AnyAsync(
            character => character.AccountId == accountId,
            cancellationToken))
        {
            return CharacterCreationResult.Failure(
                CharacterCreationErrorCodes.AlreadyExists);
        }

        return CharacterCreationResult.Failure(CharacterCreationErrorCodes.NameTaken);
    }

    private bool HasDefinition(string type, string id) =>
        _contentPackage.Definitions.Any(
            definition => string.Equals(definition.Type, type, StringComparison.Ordinal)
                && string.Equals(definition.Id, id, StringComparison.Ordinal));

    private static bool Matches(
        Character character,
        Guid accountId,
        CreateCharacterCommand command,
        CharacterNameValidationResult name) =>
        character.AccountId == accountId
        && string.Equals(character.Name, name.DisplayName, StringComparison.Ordinal)
        && string.Equals(character.NormalizedName, name.NormalizedName, StringComparison.Ordinal)
        && string.Equals(character.RaceId, command.RaceId, StringComparison.Ordinal)
        && string.Equals(character.GenderId, command.GenderId, StringComparison.Ordinal)
        && string.Equals(character.ClassId, command.ClassId, StringComparison.Ordinal);

    private static bool IsCreationConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: AccountConstraint or CreationRequestConstraint or NormalizedNameConstraint
        };
}
