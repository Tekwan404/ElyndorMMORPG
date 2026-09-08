using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.World;
using Elyndor.Core.Quests;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.World;

public static class WorldContractErrorCodes
{
    public const string CharacterNotFound = "world_contract_character_not_found";
    public const string ContractNotFound = "world_contract_not_found";
    public const string LevelRequired = "world_contract_level_required";
    public const string InvalidLocation = "world_contract_invalid_location";
    public const string PrerequisiteRequired = "world_contract_prerequisite_required";
    public const string AlreadyCompleted = "world_contract_already_completed";
    public const string Travelling = "world_contract_travelling";
}

public sealed record WorldContractAcceptResult(
    bool IsSuccess,
    string? ErrorCode,
    string? ContractId)
{
    public static WorldContractAcceptResult Success(string contractId) =>
        new(true, null, contractId);

    public static WorldContractAcceptResult Failure(string errorCode) =>
        new(false, errorCode, null);
}

public sealed class WorldContractService(
    GameDbContext dbContext,
    IContentSnapshotProvider contentProvider,
    TimeProvider timeProvider)
{
    public async Task<WorldContractAcceptResult> AcceptAsync(
        Guid accountId,
        string contractId,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty)
            return WorldContractAcceptResult.Failure(WorldContractErrorCodes.CharacterNotFound);
        if (string.IsNullOrWhiteSpace(contractId))
            return WorldContractAcceptResult.Failure(WorldContractErrorCodes.ContractNotFound);

        Character? character = await dbContext.Characters
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.AccountId == accountId,
                cancellationToken);
        if (character is null)
            return WorldContractAcceptResult.Failure(WorldContractErrorCodes.CharacterNotFound);

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (await TravelPersistence.IsTravellingAsync(
                dbContext,
                character.Id,
                now,
                cancellationToken))
        {
            return WorldContractAcceptResult.Failure(
                WorldContractErrorCodes.Travelling);
        }

        GameContentPackage content = contentProvider.GetCurrent().Package;
        WorldContractDefinition? contract = (content.WorldContracts ?? [])
            .SingleOrDefault(candidate =>
                string.Equals(candidate.Id, contractId, StringComparison.Ordinal));
        if (contract is null)
            return WorldContractAcceptResult.Failure(WorldContractErrorCodes.ContractNotFound);
        if (character.Level < contract.RequiredLevel)
            return WorldContractAcceptResult.Failure(WorldContractErrorCodes.LevelRequired);

        bool completed = await dbContext.CharacterContractCompletions
            .AsNoTracking()
            .AnyAsync(
                state => state.CharacterId == character.Id
                    && state.ContractId == contract.Id,
                cancellationToken);
        if (completed)
            return WorldContractAcceptResult.Failure(WorldContractErrorCodes.AlreadyCompleted);

        if (!string.IsNullOrWhiteSpace(contract.OfferLocationId))
        {
            string? currentLocation = await dbContext.CharacterLocations
                .AsNoTracking()
                .Where(state => state.CharacterId == character.Id)
                .Select(state => state.LocationId)
                .SingleOrDefaultAsync(cancellationToken);
            if (!string.Equals(
                    currentLocation,
                    contract.OfferLocationId,
                    StringComparison.Ordinal))
            {
                return WorldContractAcceptResult.Failure(
                    WorldContractErrorCodes.InvalidLocation);
            }
        }

        QuestDefinition? quest = QuestCatalog.Find(content, contract.Id);
        foreach (string prerequisite in quest?.PrerequisiteQuestIds ?? [])
        {
            bool prerequisiteCompleted =
                await dbContext.QuestRewardGrants.AsNoTracking().AnyAsync(
                    grant => grant.CharacterId == character.Id
                        && grant.QuestId == prerequisite,
                    cancellationToken)
                || await dbContext.CharacterQuestStates.AsNoTracking().AnyAsync(
                    state => state.CharacterId == character.Id
                        && state.QuestId == prerequisite
                        && state.Status == QuestStateStatuses.Completed,
                    cancellationToken)
                || await dbContext.CharacterContractCompletions.AsNoTracking().AnyAsync(
                    state => state.CharacterId == character.Id
                        && state.ContractId == prerequisite,
                    cancellationToken);
            if (!prerequisiteCompleted)
            {
                return WorldContractAcceptResult.Failure(
                    WorldContractErrorCodes.PrerequisiteRequired);
            }
        }

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO game.character_contract_acceptances
                 ("CharacterId", "ContractId", "AcceptedAtUtc")
             VALUES
                 ({character.Id}, {contract.Id}, {now})
             ON CONFLICT ("CharacterId", "ContractId") DO NOTHING
             """,
            cancellationToken);

        bool hasQuestState = await dbContext.CharacterQuestStates
            .AnyAsync(
                state => state.CharacterId == character.Id
                    && state.QuestId == contract.Id,
                cancellationToken);
        if (!hasQuestState)
        {
            dbContext.CharacterQuestStates.Add(
                new CharacterQuestState(character.Id, contract.Id, now));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return WorldContractAcceptResult.Success(contract.Id);
    }
}
