using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Talents;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Elyndor.Infrastructure.Talents;

public sealed record TalentStateSnapshot(
    Character Character,
    TalentTreeDefinition Tree,
    CharacterTalentState State,
    IReadOnlyDictionary<string, int> Loadout1Ranks,
    IReadOnlyDictionary<string, int> Loadout2Ranks);

public sealed record TalentOperationResult(bool IsSuccess, string? ErrorCode, TalentStateSnapshot? Snapshot)
{
    public static TalentOperationResult Success(TalentStateSnapshot snapshot) => new(true, null, snapshot);
    public static TalentOperationResult Failure(string code) => new(false, code, null);
}

public sealed class TalentService(
    GameDbContext dbContext,
    IContentSnapshotProvider contentProvider,
    TimeProvider timeProvider)
{
    private const string LearnOperation = "TALENT_LEARN";
    private const string SwitchOperation = "TALENT_SWITCH";
    private const string ResetOperation = "TALENT_RESET";

    private readonly CharacterDerivedStateService derivedStateService =
        new(dbContext, contentProvider);

    public TalentService(
        GameDbContext dbContext,
        GameContentPackage content,
        TimeProvider timeProvider)
        : this(
            dbContext,
            new StaticContentSnapshotProvider(content),
            timeProvider)
    {
    }

    public Task<TalentOperationResult> GetAsync(
        Guid accountId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            Character? character = await LockCharacterAsync(accountId, cancellationToken);
            if (character is null)
                return TalentOperationResult.Failure("character_not_found");

            return await GetOrCreateCoreAsync(
                character,
                saveCreated: true,
                cancellationToken);
        });

    public Task<TalentOperationResult> LearnAsync(
        Guid accountId,
        string loadoutId,
        string talentId,
        long expectedStateVersion,
        string mutationId,
        CancellationToken cancellationToken) =>
        ExecuteMutationAsync(
            accountId,
            mutationId,
            LearnOperation,
            Fingerprint(
                LearnOperation,
                loadoutId,
                talentId,
                expectedStateVersion.ToString(CultureInfo.InvariantCulture)),
            async snapshot =>
            {
                if (!TalentLoadoutIds.IsValid(loadoutId))
                    return TalentOperationResult.Failure(TalentErrorCodes.InvalidLoadout);
                if (snapshot.State.StateVersion != expectedStateVersion)
                    return TalentOperationResult.Failure(TalentErrorCodes.Conflict);

                IReadOnlyDictionary<string, int> ranks = snapshot.State.GetRanks(loadoutId);
                TalentLearnResult learned = TalentRules.TryLearn(
                    snapshot.Tree,
                    snapshot.Character.Level,
                    ranks,
                    talentId);
                if (!learned.IsSuccess)
                    return TalentOperationResult.Failure(learned.ErrorCode!);

                CharacterDerivedState oldDerivedState = await derivedStateService.ResolveAsync(
                    snapshot.Character.Id,
                    snapshot.Character.ClassId,
                    snapshot.Character.Level,
                    cancellationToken);
                DateTimeOffset now = timeProvider.GetUtcNow();
                snapshot.State.ReplaceRanks(loadoutId, learned.SelectedRanks, now, mutationId);
                await SaveWithDerivedVitalsAsync(
                    snapshot.Character,
                    oldDerivedState,
                    now,
                    cancellationToken);

                return TalentOperationResult.Success(
                    ToSnapshot(snapshot.Character, snapshot.Tree, snapshot.State));
            },
            cancellationToken);

    public Task<TalentOperationResult> SwitchAsync(
        Guid accountId,
        string loadoutId,
        long expectedStateVersion,
        string mutationId,
        CancellationToken cancellationToken) =>
        ExecuteMutationAsync(
            accountId,
            mutationId,
            SwitchOperation,
            Fingerprint(
                SwitchOperation,
                loadoutId,
                expectedStateVersion.ToString(CultureInfo.InvariantCulture)),
            async snapshot =>
            {
                if (!TalentLoadoutIds.IsValid(loadoutId))
                    return TalentOperationResult.Failure(TalentErrorCodes.InvalidLoadout);
                if (snapshot.State.StateVersion != expectedStateVersion)
                    return TalentOperationResult.Failure(TalentErrorCodes.Conflict);
                if (TalentRules.ValidateBuild(
                        snapshot.Tree,
                        snapshot.Character.Level,
                        snapshot.State.GetRanks(loadoutId)).Count > 0)
                {
                    return TalentOperationResult.Failure(TalentErrorCodes.Unavailable);
                }

                CharacterDerivedState oldDerivedState = await derivedStateService.ResolveAsync(
                    snapshot.Character.Id,
                    snapshot.Character.ClassId,
                    snapshot.Character.Level,
                    cancellationToken);
                DateTimeOffset now = timeProvider.GetUtcNow();
                snapshot.State.SwitchLoadout(loadoutId, now, mutationId);
                await SaveWithDerivedVitalsAsync(
                    snapshot.Character,
                    oldDerivedState,
                    now,
                    cancellationToken);

                return TalentOperationResult.Success(
                    ToSnapshot(snapshot.Character, snapshot.Tree, snapshot.State));
            },
            cancellationToken);

    public Task<TalentOperationResult> ResetAsync(
        Guid accountId,
        string loadoutId,
        long expectedStateVersion,
        string mutationId,
        CancellationToken cancellationToken) =>
        ExecuteMutationAsync(
            accountId,
            mutationId,
            ResetOperation,
            Fingerprint(
                ResetOperation,
                loadoutId,
                expectedStateVersion.ToString(CultureInfo.InvariantCulture)),
            async snapshot =>
            {
                if (!TalentLoadoutIds.IsValid(loadoutId))
                    return TalentOperationResult.Failure(TalentErrorCodes.InvalidLoadout);
                if (snapshot.State.StateVersion != expectedStateVersion)
                    return TalentOperationResult.Failure(TalentErrorCodes.Conflict);

                CharacterDerivedState oldDerivedState = await derivedStateService.ResolveAsync(
                    snapshot.Character.Id,
                    snapshot.Character.ClassId,
                    snapshot.Character.Level,
                    cancellationToken);
                DateTimeOffset now = timeProvider.GetUtcNow();
                snapshot.State.Reset(loadoutId, now, mutationId);
                await SaveWithDerivedVitalsAsync(
                    snapshot.Character,
                    oldDerivedState,
                    now,
                    cancellationToken);

                return TalentOperationResult.Success(
                    ToSnapshot(snapshot.Character, snapshot.Tree, snapshot.State));
            },
            cancellationToken);

    private async Task<TalentOperationResult> ExecuteMutationAsync(
        Guid accountId,
        string mutationId,
        string operationType,
        string requestFingerprint,
        Func<TalentStateSnapshot, Task<TalentOperationResult>> mutation,
        CancellationToken cancellationToken)
    {
        if (!TryParseMutationId(mutationId, out Guid parsedMutationId))
            return TalentOperationResult.Failure(TalentErrorCodes.InvalidMutationId);

        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using IDbContextTransaction transaction =
                await dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                Character? character = await LockCharacterAsync(accountId, cancellationToken);
                if (character is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return TalentOperationResult.Failure("character_not_found");
                }

                CharacterMutation? existing = await dbContext.CharacterMutations
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        candidate => candidate.CharacterId == character.Id
                            && candidate.MutationId == parsedMutationId,
                        cancellationToken);

                TalentOperationResult loaded = await GetOrCreateCoreAsync(
                    character,
                    saveCreated: existing is not null,
                    cancellationToken);
                if (!loaded.IsSuccess)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return loaded;
                }

                TalentStateSnapshot snapshot = loaded.Snapshot!;
                if (existing is not null)
                {
                    if (!string.Equals(existing.OperationType, operationType, StringComparison.Ordinal)
                        || !string.Equals(
                            existing.RequestFingerprint,
                            requestFingerprint,
                            StringComparison.Ordinal))
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return TalentOperationResult.Failure(
                            TalentErrorCodes.MutationConflict);
                    }

                    await transaction.CommitAsync(cancellationToken);
                    return TalentOperationResult.Success(
                        ToSnapshot(snapshot.Character, snapshot.Tree, snapshot.State));
                }

                dbContext.CharacterMutations.Add(new CharacterMutation(
                    character.Id,
                    parsedMutationId,
                    operationType,
                    requestFingerprint,
                    timeProvider.GetUtcNow()));

                TalentOperationResult result = await mutation(snapshot);
                if (!result.IsSuccess)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return result;
                }

                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return TalentOperationResult.Failure(TalentErrorCodes.Conflict);
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return TalentOperationResult.Failure(TalentErrorCodes.Conflict);
            }
        });
    }

    private async Task SaveWithDerivedVitalsAsync(
        Character character,
        CharacterDerivedState oldDerivedState,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Persist the talent state and its durable CharacterMutation in the same transaction
        // before resolving the new authoritative derived state. A later failure still rolls
        // back both the mutation identity and gameplay changes.
        await dbContext.SaveChangesAsync(cancellationToken);

        CharacterDerivedState newDerivedState = await derivedStateService.ResolveAsync(
            character.Id,
            character.ClassId,
            character.Level,
            cancellationToken);
        CharacterVitals vitals = await dbContext.CharacterVitals.SingleAsync(
            candidate => candidate.CharacterId == character.Id,
            cancellationToken);
        CharacterVitalsScaler.ScaleToDerivedMaximums(
            vitals,
            oldDerivedState.Stats.MaxHp,
            newDerivedState.Stats.MaxHp,
            oldDerivedState.EffectiveResourceProfile.MaxValue,
            newDerivedState.EffectiveResourceProfile.MaxValue,
            now);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private Task<TalentOperationResult> ExecuteAsync(
        Func<Task<TalentOperationResult>> operation)
    {
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using IDbContextTransaction transaction =
                await dbContext.Database.BeginTransactionAsync();
            try
            {
                TalentOperationResult result = await operation();
                await transaction.CommitAsync();
                return result;
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                return TalentOperationResult.Failure(TalentErrorCodes.Conflict);
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();
                return TalentOperationResult.Failure(TalentErrorCodes.Conflict);
            }
        });
    }

    private async Task<TalentOperationResult> GetOrCreateCoreAsync(
        Character character,
        bool saveCreated,
        CancellationToken cancellationToken)
    {
        TalentTreeDefinition? tree = contentProvider.GetCurrent().Indexes.TalentTreesByClassId
            .GetValueOrDefault(character.ClassId);
        if (tree is null)
            return TalentOperationResult.Failure(TalentErrorCodes.Unavailable);

        CharacterTalentState? state = await dbContext.CharacterTalentStates
            .SingleOrDefaultAsync(
                candidate => candidate.CharacterId == character.Id,
                cancellationToken);
        bool changed = false;
        if (state is null)
        {
            state = new CharacterTalentState(
                character.Id,
                tree.Id,
                tree.Version,
                timeProvider.GetUtcNow());
            dbContext.CharacterTalentStates.Add(state);
            changed = true;
        }
        else if (!string.Equals(state.TalentTreeId, tree.Id, StringComparison.Ordinal))
        {
            state.Reinitialize(tree.Id, tree.Version, timeProvider.GetUtcNow());
            changed = true;
        }

        if (changed && saveCreated)
            await dbContext.SaveChangesAsync(cancellationToken);

        return TalentOperationResult.Success(
            ToSnapshot(character, tree, state));
    }

    private Task<Character?> LockCharacterAsync(
        Guid accountId,
        CancellationToken cancellationToken) =>
        dbContext.Characters
            .FromSqlInterpolated(
                $"SELECT * FROM game.characters WHERE \"AccountId\" = {accountId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    private static TalentStateSnapshot ToSnapshot(
        Character character,
        TalentTreeDefinition tree,
        CharacterTalentState state) =>
        new(
            character,
            tree,
            state,
            state.GetRanks(TalentLoadoutIds.Loadout1),
            state.GetRanks(TalentLoadoutIds.Loadout2));

    private static bool TryParseMutationId(
        string mutationId,
        out Guid parsedMutationId)
    {
        parsedMutationId = Guid.Empty;
        return !string.IsNullOrWhiteSpace(mutationId)
            && mutationId.Length <= 64
            && Guid.TryParse(mutationId, out parsedMutationId)
            && parsedMutationId != Guid.Empty;
    }

    private static string Fingerprint(params string[] parts)
    {
        string canonical = string.Join(
            "\u001F",
            parts.Select(part =>
                $"{part.Length.ToString(CultureInfo.InvariantCulture)}:{part}"));
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
