using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Talents;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.Talents;

public sealed record TalentAllocationCommand(string TalentId);

public sealed record TalentAllocationResult(
    bool IsSuccess,
    CharacterTalents? Talents,
    string? ErrorCode,
    string? Message)
{
    public static TalentAllocationResult Success(CharacterTalents talents) =>
        new(true, talents, null, null);

    public static TalentAllocationResult Failure(string errorCode, string message) =>
        new(false, null, errorCode, message);
}

public static class TalentAllocationErrorCodes
{
    public const string TalentTreeNotFound = "talent_tree_not_found";
    public const string TalentNotFound = "talent_not_found";
    public const string LevelRequirementNotMet = "level_requirement_not_met";
    public const string NoAvailablePoints = "no_available_points";
    public const string BranchRequirementNotMet = "branch_requirement_not_met";
    public const string PrerequisiteNotMet = "prerequisite_not_met";
    public const string PrerequisiteNotMaxed = "prerequisite_not_maxed";
    public const string TalentMaxRank = "talent_max_rank";
    public const string CharacterNotFound = "character_not_found";
}

public sealed class TalentAllocationService(
    GameDbContext dbContext,
    TimeProvider timeProvider,
    GameContentPackage contentPackage)
{
    private readonly GameDbContext _dbContext =
        dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly GameContentPackage _contentPackage =
        contentPackage ?? throw new ArgumentNullException(nameof(contentPackage));

    public async Task<TalentAllocationResult> AllocateTalentAsync(
        Guid characterId,
        TalentAllocationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Get character
        Character? character = await _dbContext.Characters
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == characterId, cancellationToken);

        if (character is null)
        {
            return TalentAllocationResult.Failure(
                TalentAllocationErrorCodes.CharacterNotFound,
                $"Character {characterId} not found");
        }

        // Determine talent tree based on class
        string talentTreeId = $"{character.ClassId}_GUARDIAN"; // Simplified: assumes Guardian spec
        var talentTree = TalentContentService.GetTalentTree(talentTreeId);

        if (talentTree is null)
        {
            // Fallback: try to find any tree for this class
            var allTrees = TalentContentService.GetAllTalentTrees();
            talentTree = allTrees.FirstOrDefault(t => t.ClassId == character.ClassId);

            if (talentTree is null)
            {
                return TalentAllocationResult.Failure(
                    TalentAllocationErrorCodes.TalentTreeNotFound,
                    $"No talent tree found for class {character.ClassId}");
            }

            talentTreeId = talentTree.TalentTreeId;
        }

        // Get or create character talents
        CharacterTalents? existingTalents = await _dbContext.CharacterTalents
            .AsNoTracking()
            .SingleOrDefaultAsync(ct => ct.CharacterId == characterId, cancellationToken);

        CharacterTalents currentTalents = existingTalents ?? new CharacterTalents(
            characterId,
            talentTreeId,
            new Dictionary<string, int>().AsReadOnly(),
            0,
            Math.Max(0, character.Level - 1),
            _timeProvider.GetUtcNow());

        // Validate allocation
        var validationResult = TalentCalculator.CanAllocateTalent(
            currentTalents,
            command.TalentId,
            talentTree,
            character.Level);

        if (!validationResult.Success)
        {
            return TalentAllocationResult.Failure(
                validationResult.ErrorCode ?? "validation_failed",
                validationResult.Message ?? "Talent allocation validation failed");
        }

        // Allocate the point
        var allocationResult = TalentCalculator.AllocateTalentPoint(
            currentTalents,
            command.TalentId,
            talentTree,
            character.Level,
            _timeProvider);

        if (!allocationResult.Success || allocationResult.UpdatedTalents is null)
        {
            return TalentAllocationResult.Failure(
                allocationResult.ErrorCode ?? "allocation_failed",
                allocationResult.Message ?? "Failed to allocate talent point");
        }

        // Save to database
        if (existingTalents is null)
        {
            _dbContext.CharacterTalents.Add(allocationResult.UpdatedTalents);
        }
        else
        {
            _dbContext.Entry(existingTalents).CurrentValues.SetValues(allocationResult.UpdatedTalents);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return TalentAllocationResult.Success(allocationResult.UpdatedTalents);
    }

    public async Task<CharacterTalents?> GetCharacterTalentsAsync(
        Guid characterId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.CharacterTalents
            .AsNoTracking()
            .SingleOrDefaultAsync(ct => ct.CharacterId == characterId, cancellationToken);
    }

    public async Task<TalentTreeProfile?> GetCharacterTalentTreeAsync(
        Guid characterId,
        CancellationToken cancellationToken)
    {
        Character? character = await _dbContext.Characters
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == characterId, cancellationToken);

        if (character is null)
        {
            return null;
        }

        // Try to get existing talent tree assignment
        CharacterTalents? existingTalents = await _dbContext.CharacterTalents
            .AsNoTracking()
            .SingleOrDefaultAsync(ct => ct.CharacterId == characterId, cancellationToken);

        string talentTreeId = existingTalents?.TalentTreeId ?? $"{character.ClassId}_GUARDIAN";
        var talentTree = TalentContentService.GetTalentTree(talentTreeId);

        if (talentTree is null)
        {
            var allTrees = TalentContentService.GetAllTalentTrees();
            talentTree = allTrees.FirstOrDefault(t => t.ClassId == character.ClassId);
        }

        return talentTree;
    }

    public PrimaryStats CalculateTalentBonuses(CharacterTalents talents, string talentTreeId)
    {
        var talentTree = TalentContentService.GetTalentTree(talentTreeId);
        if (talentTree is null)
        {
            return new PrimaryStats(0, 0, 0, 0);
        }

        return TalentCalculator.CalculateTalentStatBonuses(talents, talentTree);
    }
}
