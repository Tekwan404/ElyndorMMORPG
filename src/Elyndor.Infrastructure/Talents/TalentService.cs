using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Talents;
using Elyndor.Infrastructure.Characters;
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
    GameContentPackage content,
    TimeProvider timeProvider)
{
    private readonly CharacterDerivedStateService derivedStateService = new(dbContext, content);
    public Task<TalentOperationResult> GetAsync(Guid accountId, CancellationToken cancellationToken) =>
        ExecuteAsync(() => GetOrCreateCoreAsync(accountId, cancellationToken));

    public Task<TalentOperationResult> LearnAsync(
        Guid accountId, string loadoutId, string talentId, long expectedStateVersion, string mutationId,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
    {
        TalentOperationResult loaded = await GetOrCreateCoreAsync(accountId, cancellationToken, false);
        if (!loaded.IsSuccess) return loaded;
        TalentStateSnapshot snapshot = loaded.Snapshot!;
        if (!IsValidMutationId(mutationId)) return TalentOperationResult.Failure(TalentErrorCodes.InvalidMutationId);
        if (!TalentLoadoutIds.IsValid(loadoutId)) return TalentOperationResult.Failure(TalentErrorCodes.InvalidLoadout);
        if (snapshot.State.HasProcessedMutation(mutationId)) return TalentOperationResult.Success(snapshot);
        if (snapshot.State.StateVersion != expectedStateVersion) return TalentOperationResult.Failure(TalentErrorCodes.Conflict);
        IReadOnlyDictionary<string, int> ranks = snapshot.State.GetRanks(loadoutId);
        TalentLearnResult learned = TalentRules.TryLearn(snapshot.Tree, snapshot.Character.Level, ranks, talentId);
        if (!learned.IsSuccess) return TalentOperationResult.Failure(learned.ErrorCode!);
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
        return TalentOperationResult.Success(ToSnapshot(snapshot.Character, snapshot.Tree, snapshot.State));
    });

    public Task<TalentOperationResult> SwitchAsync(
        Guid accountId, string loadoutId, long expectedStateVersion, string mutationId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            TalentOperationResult loaded = await GetOrCreateCoreAsync(accountId, cancellationToken, false);
            if (!loaded.IsSuccess) return loaded;
            TalentStateSnapshot snapshot = loaded.Snapshot!;
            if (!IsValidMutationId(mutationId)) return TalentOperationResult.Failure(TalentErrorCodes.InvalidMutationId);
            if (!TalentLoadoutIds.IsValid(loadoutId)) return TalentOperationResult.Failure(TalentErrorCodes.InvalidLoadout);
            if (snapshot.State.HasProcessedMutation(mutationId)) return TalentOperationResult.Success(snapshot);
            if (snapshot.State.StateVersion != expectedStateVersion) return TalentOperationResult.Failure(TalentErrorCodes.Conflict);
            if (TalentRules.ValidateBuild(snapshot.Tree, snapshot.Character.Level, snapshot.State.GetRanks(loadoutId)).Count > 0)
                return TalentOperationResult.Failure(TalentErrorCodes.Unavailable);
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
            return TalentOperationResult.Success(ToSnapshot(snapshot.Character, snapshot.Tree, snapshot.State));
        });

    public Task<TalentOperationResult> ResetAsync(
        Guid accountId, string loadoutId, long expectedStateVersion, string mutationId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            TalentOperationResult loaded = await GetOrCreateCoreAsync(accountId, cancellationToken, false);
            if (!loaded.IsSuccess) return loaded;
            TalentStateSnapshot snapshot = loaded.Snapshot!;
            if (!IsValidMutationId(mutationId)) return TalentOperationResult.Failure(TalentErrorCodes.InvalidMutationId);
            if (!TalentLoadoutIds.IsValid(loadoutId)) return TalentOperationResult.Failure(TalentErrorCodes.InvalidLoadout);
            if (snapshot.State.HasProcessedMutation(mutationId)) return TalentOperationResult.Success(snapshot);
            if (snapshot.State.StateVersion != expectedStateVersion) return TalentOperationResult.Failure(TalentErrorCodes.Conflict);
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
            return TalentOperationResult.Success(ToSnapshot(snapshot.Character, snapshot.Tree, snapshot.State));
        });

    private async Task SaveWithDerivedVitalsAsync(
        Character character,
        CharacterDerivedState oldDerivedState,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Persist the talent mutation inside the current transaction before resolving the
        // new authoritative derived state. A failure later still rolls the whole transaction back.
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

    private Task<TalentOperationResult> ExecuteAsync(Func<Task<TalentOperationResult>> operation)
    {
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync();
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
        });
    }

    private async Task<TalentOperationResult> GetOrCreateCoreAsync(
        Guid accountId, CancellationToken cancellationToken, bool saveCreated = true)
    {
        Character? character = await dbContext.Characters.SingleOrDefaultAsync(
            candidate => candidate.AccountId == accountId, cancellationToken);
        if (character is null) return TalentOperationResult.Failure("character_not_found");
        TalentTreeDefinition? tree = content.TalentTrees?.SingleOrDefault(candidate => candidate.ClassId == character.ClassId);
        if (tree is null) return TalentOperationResult.Failure(TalentErrorCodes.Unavailable);
        CharacterTalentState? state = await dbContext.CharacterTalentStates.SingleOrDefaultAsync(
            candidate => candidate.CharacterId == character.Id, cancellationToken);
        bool changed = false;
        if (state is null)
        {
            state = new CharacterTalentState(character.Id, tree.Id, tree.Version, timeProvider.GetUtcNow());
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

        return TalentOperationResult.Success(ToSnapshot(character, tree, state));
    }

    private static TalentStateSnapshot ToSnapshot(
        Character character, TalentTreeDefinition tree, CharacterTalentState state) =>
        new(character, tree, state, state.GetRanks(TalentLoadoutIds.Loadout1), state.GetRanks(TalentLoadoutIds.Loadout2));

    private static bool IsValidMutationId(string mutationId) =>
        !string.IsNullOrWhiteSpace(mutationId) && mutationId.Length <= 64;
}
