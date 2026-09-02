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
    IReadOnlyList<string> KnownAbilityIds,
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
    private const string StarterTownId = "STARTER_TOWN";

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
        ResolvedTalentModifiers talentModifiers = ResolvedTalentModifiers.Empty;
        TalentTreeDefinition? talentTree = contentPackage.TalentTrees?
            .SingleOrDefault(tree => tree.ClassId == character.ClassId);
        if (talentTree is not null)
        {
            CharacterTalentState? talentState = await dbContext.CharacterTalentStates
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.CharacterId == character.Id, cancellationToken);
            if (talentState is not null)
            {
                talentModifiers = TalentModifierResolver.Resolve(
                    talentTree, talentState.GetRanks(talentState.ActiveLoadoutId));
                talentPercentages = new TalentPrimaryStatPercentages(
                    talentModifiers.Stats.StrengthPercent,
                    0,
                    0,
                    talentModifiers.Stats.StaminaPercent);
            }
        }
        CharacterStats stats = new CharacterStatCalculator(
            contentPackage.StatFormula
                ?? throw new InvalidOperationException("Stat formula content is required."),
            contentPackage.ClassProfiles).Calculate(
                character.ClassId,
                character.Level,
                CharacterStatInputs.Empty with
                {
                    TalentPercentages = talentPercentages,
                    TalentDerived = talentModifiers.Stats
                });
        string[] knownAbilityIds = (classProfile.StartingAbilityIds ?? [])
            .Concat((classProfile.AbilityUnlocks ?? [])
                .Where(unlock => unlock.UnlockLevel <= character.Level)
                .Select(unlock => unlock.AbilityId))
            .Concat(talentModifiers.UnlockedAbilityIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(abilityId => abilityId, StringComparer.Ordinal)
            .ToArray();
        ResourceProfile effectiveResourceProfile = resourceProfile with
        {
            MaxValue = resourceProfile.MaxValue + talentModifiers.Stats.MaxResourceFlat
        };
        CharacterVitals vitals = await dbContext.CharacterVitals
            .SingleAsync(
                candidate => candidate.CharacterId == character.Id,
                cancellationToken);
        TimeSpan elapsed = now - vitals.CheckpointedAtUtc;
        TimeSpan contextElapsed = now - vitals.ContextStartedAtUtc;
        decimal currentResource = CharacterResourceRules.ApplyElapsed(
            effectiveResourceProfile,
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

        // Prototype safe-zone rule: Starter Town is the recovery checkpoint.
        // The mutation remains server-authoritative and is persisted so reloads keep the recovered state.
        if (string.Equals(current.Id, StarterTownId, StringComparison.Ordinal))
        {
            currentHp = stats.MaxHp;
            currentResource = decimal.Clamp(
                resourceProfile.RespawnValue,
                0,
                effectiveResourceProfile.MaxValue);
            vitals.Checkpoint(currentHp, currentResource, now);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

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
                knownAbilityIds,
                stats,
                new BootstrapVitals(
                    currentHp,
                    stats.MaxHp,
                    resourceProfile.Id,
                    currentResource,
                    effectiveResourceProfile.MaxValue,
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
