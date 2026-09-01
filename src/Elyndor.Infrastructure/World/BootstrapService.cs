using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.World;
using Elyndor.Core.Talents;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.World;

public sealed record BootstrapCharacter(
    Guid Id,
    string Name,
    string RaceId,
    string GenderId,
    string ClassId,
    int Level,
    string PrimaryAttribute,
    string ClassProfileVersion,
    CharacterStats Stats,
    BootstrapVitals Vitals);

public sealed record BootstrapVitals(
    decimal CurrentHp,
    decimal MaxHp,
    string ResourceType,
    decimal CurrentResource,
    decimal MaxResource,
    DateTimeOffset CheckpointedAtUtc);

public sealed record BootstrapLocation(
    string Id,
    string DisplayName,
    string DangerLevel,
    int RecommendedLevel);

public sealed record BootstrapWorld(
    BootstrapLocation CurrentLocation,
    long Version,
    IReadOnlyList<BootstrapLocation> OutgoingTransitions);

public sealed record BootstrapSnapshot(
    Guid AccountId,
    BootstrapCharacter? Character,
    BootstrapWorld? World,
    string ContentVersion,
    string BalanceVersion,
    DateTimeOffset ServerTimeUtc);

public sealed class BootstrapService(
    GameDbContext dbContext,
    GameContentPackage contentPackage,
    WorldMap worldMap,
    TimeProvider timeProvider)
{
    public async Task<BootstrapSnapshot> GetAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        Character? character = await dbContext.Characters
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.AccountId == accountId,
                cancellationToken);
        if (character is null)
        {
            return new BootstrapSnapshot(
                accountId,
                null,
                null,
                contentPackage.ContentVersion,
                contentPackage.BalanceVersion,
                timeProvider.GetUtcNow());
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        ClassProfile classProfile = (contentPackage.ClassProfiles
            ?? throw new InvalidOperationException("Class profiles are required."))
            .Single(profile => string.Equals(profile.Id, character.ClassId, StringComparison.Ordinal));
        ResourceProfile resourceProfile = (contentPackage.ResourceProfiles
            ?? throw new InvalidOperationException("Resource profiles are required."))
            .Single(profile => string.Equals(
                profile.Id,
                classProfile.ResourceProfileId,
                StringComparison.Ordinal));
        TalentPrimaryStatPercentages talentPercentages = TalentPrimaryStatPercentages.Empty;
        TalentTreeDefinition? talentTree = contentPackage.TalentTrees?
            .SingleOrDefault(tree => tree.ClassId == character.ClassId);
        if (talentTree is not null)
        {
            CharacterTalentState? talentState = await dbContext.CharacterTalentStates
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.CharacterId == character.Id, cancellationToken);
            if (talentState is not null)
            {
                talentPercentages = TalentStatModifierResolver.ResolvePrimaryPercentages(
                    talentTree, talentState.GetRanks(talentState.ActiveLoadoutId));
            }
        }
        CharacterStats stats = new CharacterStatCalculator(
            contentPackage.StatFormula
                ?? throw new InvalidOperationException("Stat formula content is required."),
            contentPackage.ClassProfiles).Calculate(
                character.ClassId,
                character.Level,
                CharacterStatInputs.Empty with { TalentPercentages = talentPercentages });
        CharacterVitals vitals = await dbContext.CharacterVitals
            .SingleAsync(
                candidate => candidate.CharacterId == character.Id,
                cancellationToken);
        TimeSpan elapsed = now - vitals.CheckpointedAtUtc;
        TimeSpan contextElapsed = now - vitals.ContextStartedAtUtc;
        decimal currentResource = CharacterResourceRules.ApplyElapsed(
            resourceProfile,
            vitals.CurrentResource,
            elapsed,
            isInCombat: false,
            contextElapsed);
        decimal currentHp = decimal.Clamp(vitals.CurrentHp, 0, stats.MaxHp);
        vitals.Checkpoint(currentHp, currentResource, now);
        await dbContext.SaveChangesAsync(cancellationToken);

        CharacterLocation location = await dbContext.CharacterLocations
            .AsNoTracking()
            .SingleAsync(
                candidate => candidate.CharacterId == character.Id,
                cancellationToken);
        LocationDefinition current = worldMap.GetRequired(location.LocationId);
        BootstrapLocation[] transitions = current.Transitions
            .Select(worldMap.GetRequired)
            .Select(ToLocation)
            .ToArray();

        return new BootstrapSnapshot(
            accountId,
            new BootstrapCharacter(
                character.Id,
                character.Name,
                character.RaceId,
                character.GenderId,
                character.ClassId,
                character.Level,
                classProfile.PrimaryAttribute,
                contentPackage.BalanceVersion,
                stats,
                new BootstrapVitals(
                    currentHp,
                    stats.MaxHp,
                    resourceProfile.Id,
                    currentResource,
                    resourceProfile.MaxValue,
                    now)),
            new BootstrapWorld(ToLocation(current), location.Version, transitions),
            contentPackage.ContentVersion,
            contentPackage.BalanceVersion,
            now);
    }

    private static BootstrapLocation ToLocation(LocationDefinition location) =>
        new(
            location.Id,
            location.DisplayName,
            location.DangerLevel,
            location.RecommendedLevel);
}
