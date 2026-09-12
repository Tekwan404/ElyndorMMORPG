using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.Characters;

public sealed record CompanionProfileSelection(
    string Id,
    string Name,
    string Archetype,
    string? ArtId);

public sealed record CharacterCompanionSnapshot(
    string SelectedPhysicalProfileId,
    string EffectiveProfileId,
    IReadOnlyList<CompanionProfileSelection> AvailableProfiles);

public sealed record CharacterCompanionSelectionResult(
    bool IsSuccess,
    CharacterCompanionSnapshot? Snapshot,
    string? ErrorCode)
{
    public static CharacterCompanionSelectionResult Success(CharacterCompanionSnapshot snapshot) =>
        new(true, snapshot, null);

    public static CharacterCompanionSelectionResult Failure(string errorCode) =>
        new(false, null, errorCode);
}

public static class CharacterCompanionErrorCodes
{
    public const string NotAvailable = "companion_not_available";
    public const string InvalidProfile = "companion_invalid_profile";
    public const string ChangeInCombat = "companion_change_in_combat";
}

public sealed class CharacterCompanionService(
    GameDbContext dbContext,
    IContentSnapshotProvider contentProvider,
    CharacterDerivedStateService derivedStateService)
{
    public async Task<CharacterCompanionSelectionResult> GetAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        Character? character = await dbContext.Characters
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.AccountId == accountId, cancellationToken);
        if (character is null || !string.Equals(character.ClassId, "ARCHER", StringComparison.Ordinal))
            return CharacterCompanionSelectionResult.Failure(CharacterCompanionErrorCodes.NotAvailable);

        return CharacterCompanionSelectionResult.Success(
            await BuildSnapshotAsync(character, cancellationToken));
    }

    public async Task<CharacterCompanionSelectionResult> SelectAsync(
        Guid accountId,
        string companionProfileId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(companionProfileId))
            return CharacterCompanionSelectionResult.Failure(CharacterCompanionErrorCodes.InvalidProfile);

        Character? character = await dbContext.Characters
            .SingleOrDefaultAsync(candidate => candidate.AccountId == accountId, cancellationToken);
        if (character is null || !string.Equals(character.ClassId, "ARCHER", StringComparison.Ordinal))
            return CharacterCompanionSelectionResult.Failure(CharacterCompanionErrorCodes.NotAvailable);

        GameContentSnapshot contentSnapshot = contentProvider.GetCurrent();
        ClassProfile classProfile = contentSnapshot.Indexes.ClassesById[character.ClassId];
        CompanionProfileDefinition? profile = PhysicalProfiles(classProfile).SingleOrDefault(candidate =>
            string.Equals(candidate.Id, companionProfileId, StringComparison.Ordinal));
        if (profile is null)
            return CharacterCompanionSelectionResult.Failure(CharacterCompanionErrorCodes.InvalidProfile);

        bool isInCombat = await dbContext.ActiveCombatSessions
            .AnyAsync(state => state.CharacterId == character.Id, cancellationToken);
        if (isInCombat)
            return CharacterCompanionSelectionResult.Failure(CharacterCompanionErrorCodes.ChangeInCombat);

        character.SelectCompanionProfile(profile.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CharacterCompanionSelectionResult.Success(
            await BuildSnapshotAsync(character, cancellationToken));
    }

    private async Task<CharacterCompanionSnapshot> BuildSnapshotAsync(
        Character character,
        CancellationToken cancellationToken)
    {
        GameContentSnapshot contentSnapshot = contentProvider.GetCurrent();
        ClassProfile classProfile = contentSnapshot.Indexes.ClassesById[character.ClassId];
        IReadOnlyList<CompanionProfileDefinition> availableProfiles = PhysicalProfiles(classProfile);
        CharacterDerivedState derived = await derivedStateService.ResolveAsync(
            character.Id,
            character.ClassId,
            character.Level,
            contentSnapshot,
            cancellationToken);
        string selectedProfileId = derived.SelectedPhysicalCompanionProfile?.Id
            ?? throw new InvalidOperationException("Archer requires a selected physical companion profile.");
        string effectiveProfileId = derived.ActiveCompanionProfile?.Id ?? selectedProfileId;

        return new CharacterCompanionSnapshot(
            selectedProfileId,
            effectiveProfileId,
            availableProfiles.Select(profile => new CompanionProfileSelection(
                profile.Id,
                profile.Name,
                profile.Archetype,
                profile.ArtId)).ToArray());
    }

    private static CompanionProfileDefinition[] PhysicalProfiles(
        ClassProfile classProfile) =>
        (classProfile.CompanionProfiles ?? [])
            .Where(profile => string.Equals(profile.Tag, "PHYSICAL_PET", StringComparison.Ordinal))
            .OrderBy(profile => profile.Id, StringComparer.Ordinal)
            .ToArray();
}
