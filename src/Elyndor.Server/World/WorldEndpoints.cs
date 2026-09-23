using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Contracts.World;
using Elyndor.Core.World;
using Elyndor.Core.Content;
using Elyndor.Core.Dungeons;
using Elyndor.Core.Items;
using Elyndor.Core.Monsters;
using Elyndor.Infrastructure.World;
using Elyndor.Infrastructure.Characters;
using Elyndor.Server.Items;

namespace Elyndor.Server.World;

public static class WorldEndpoints
{
    public static IEndpointRouteBuilder MapWorldEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1")
            .RequireAuthorization()
            .WithTags("World");

        group.MapGet("/bootstrap", GetBootstrapAsync);
        group.MapGet("/world/locations", (IContentSnapshotProvider contentProvider) =>
        {
            GameContentSnapshot content = contentProvider.GetCurrent();
            return Results.Ok(content.WorldMap.Locations
                .OrderBy(location => location.Id, StringComparer.Ordinal)
                .Select(location => ToLocation(location, content.Indexes))
                .ToArray());
        });
        group.MapPost("/world/explore", ExploreAsync);
        group.MapPost("/world/travel", TravelAsync);
        group.MapPost("/world/contracts/accept", AcceptContractAsync);

        return endpoints;
    }

    private static async Task<IResult> GetBootstrapAsync(
        ClaimsPrincipal user,
        BootstrapService bootstrapService,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
        {
            return Results.Unauthorized();
        }

        BootstrapSnapshot snapshot = await bootstrapService.GetAsync(
            accountId,
            cancellationToken);
        return Results.Ok(ToResponse(snapshot));
    }

    private static async Task<IResult> AcceptContractAsync(
        AcceptWorldContractRequest request,
        ClaimsPrincipal user,
        HttpContext httpContext,
        WorldContractService contractService,
        CharacterOperationGuard operationGuard,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        return await operationGuard.ExecuteOutOfCombatAsync(
            accountId,
            async () =>
            {
                WorldContractAcceptResult result = await contractService.AcceptAsync(
                    accountId,
                    request.ContractId,
                    cancellationToken);
                if (result.IsSuccess)
                {
                    return Results.Ok(new AcceptWorldContractResponse(
                        result.ContractId!,
                        "ACTIVE"));
                }

                int statusCode = result.ErrorCode switch
                {
                    WorldContractErrorCodes.CharacterNotFound
                        or WorldContractErrorCodes.ContractNotFound =>
                        StatusCodes.Status404NotFound,
                    WorldContractErrorCodes.LevelRequired
                        or WorldContractErrorCodes.InvalidLocation
                        or WorldContractErrorCodes.PrerequisiteRequired =>
                        StatusCodes.Status403Forbidden,
                    WorldContractErrorCodes.AlreadyCompleted
                        or WorldContractErrorCodes.Travelling =>
                        StatusCodes.Status409Conflict,
                    _ => StatusCodes.Status422UnprocessableEntity
                };
                return Results.Problem(
                    statusCode: statusCode,
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = result.ErrorCode!,
                        ["correlationId"] = httpContext.TraceIdentifier
                    });
            },
            () => InCombatProblem(httpContext),
            cancellationToken);
    }

    private static async Task<IResult> ExploreAsync(
        ClaimsPrincipal user,
        HttpContext httpContext,
        WorldEncounterService encounterService,
        CharacterOperationGuard operationGuard,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        return await operationGuard.ExecuteOutOfCombatAsync(
            accountId,
            async () =>
            {
                (WorldEncounterSnapshot? encounter, string? errorCode) =
                    await encounterService.ExploreAsync(accountId, cancellationToken);
                if (encounter is not null)
                {
                    return Results.Ok(new WorldEncounterResponse(
                        encounter.EncounterId,
                        encounter.MonsterId,
                        encounter.Name,
                        encounter.Level,
                        encounter.Rank,
                        encounter.Description,
                        encounter.ArtId));
                }

                int statusCode = errorCode switch
                {
                    WorldEncounterErrorCodes.CharacterNotFound =>
                        StatusCodes.Status404NotFound,
                    WorldEncounterErrorCodes.Travelling =>
                        StatusCodes.Status409Conflict,
                    _ => StatusCodes.Status422UnprocessableEntity
                };
                return Results.Problem(
                    statusCode: statusCode,
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = errorCode ?? WorldEncounterErrorCodes.EncounterUnavailable,
                        ["correlationId"] = httpContext.TraceIdentifier
                    });
            },
            () => InCombatProblem(httpContext),
            cancellationToken);
    }

    private static async Task<IResult> TravelAsync(
        TravelRequest request,
        ClaimsPrincipal user,
        HttpContext httpContext,
        BootstrapService bootstrapService,
        TravelService travelService,
        CharacterOperationGuard operationGuard,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
        {
            return Results.Unauthorized();
        }

        return await operationGuard.ExecuteOutOfCombatAsync(
            accountId,
            async () =>
            {
                await bootstrapService.GetAsync(
                    accountId,
                    cancellationToken,
                    checkpoint: true);

                TravelResult result = await travelService.TravelAsync(
                    accountId,
                    request.RequestId,
                    request.TargetLocationId,
                    cancellationToken);
                if (result.IsSuccess)
                {
                    return Results.Ok(new TravelResponse(
                        result.LocationId!,
                        result.Version!.Value,
                        result.IsTravelling,
                        result.TargetLocationId,
                        result.EndsAtUtc));
                }

                int statusCode = result.ErrorCode is
                    TravelErrorCodes.Conflict
                        or TravelErrorCodes.IdempotencyConflict
                        or TravelErrorCodes.InProgress
                        ? StatusCodes.Status409Conflict
                        : result.ErrorCode is TravelErrorCodes.InvalidTransition
                            or TravelErrorCodes.UnknownLocation
                            or TravelErrorCodes.InvalidRequest
                                ? StatusCodes.Status422UnprocessableEntity
                        : result.ErrorCode is TravelErrorCodes.LevelRequired
                            or TravelErrorCodes.ContractRequired
                                ? StatusCodes.Status403Forbidden
                                : StatusCodes.Status404NotFound;
                return Results.Problem(
                    statusCode: statusCode,
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = result.ErrorCode!,
                        ["correlationId"] = httpContext.TraceIdentifier
                    });
            },
            () => InCombatProblem(httpContext),
            cancellationToken);
    }

    private static IResult InCombatProblem(HttpContext context) =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = CharacterOperationErrorCodes.InCombat,
                ["correlationId"] = context.TraceIdentifier
            });

    private static BootstrapResponse ToResponse(BootstrapSnapshot snapshot) =>
        new(
            snapshot.AccountId,
            snapshot.Character is null
                ? null
                : new BootstrapCharacterResponse(
                    snapshot.Character.Id,
                    snapshot.Character.Name,
                    snapshot.Character.PublicCode,
                    snapshot.Character.RaceId,
                    snapshot.Character.GenderId,
                    snapshot.Character.ClassId,
                    snapshot.Character.Level,
                    snapshot.Character.Experience,
                    snapshot.Character.XpToNextLevel,
                    snapshot.Character.Gold,
                    snapshot.Character.PrimaryAttribute,
                    snapshot.Character.ClassProfileVersion,
                    snapshot.Character.KnownAbilityIds,
                    snapshot.Character.KnownAbilities
                        .Select(ability => new BootstrapAbilityResponse(
                            ability.Id,
                            ability.DisplayName,
                            ability.Description,
                            ability.IconId,
                            ability.ResourceCost,
                            ability.CooldownSeconds,
                            ability.Type,
                            ability.TargetType,
                            ability.SourceTalentId,
                            ability.SourceTalentName))
                        .ToArray(),
                    new CharacterStatsResponse(
                        snapshot.Character.Stats.Strength,
                        snapshot.Character.Stats.Agility,
                        snapshot.Character.Stats.Intellect,
                        snapshot.Character.Stats.Stamina,
                        snapshot.Character.Stats.MaxHp,
                        snapshot.Character.Stats.AttackPower,
                        snapshot.Character.Stats.SpellPower,
                        snapshot.Character.Stats.CriticalChance,
                        snapshot.Character.Stats.CriticalDamage,
                        snapshot.Character.Stats.Accuracy,
                        snapshot.Character.Stats.ArmorPenetration,
                        snapshot.Character.Stats.MagicPenetration,
                        snapshot.Character.Stats.AttackSpeed,
                        snapshot.Character.Stats.Armor,
                        snapshot.Character.Stats.MagicResistance,
                        snapshot.Character.Stats.Dodge),
                    snapshot.Character.StatBreakdown.ToDictionary(
                        pair => pair.Key,
                        pair => new CharacterStatBreakdownResponse(
                            pair.Value.FinalValue,
                            pair.Value.Contributions
                                .Select(contribution => new CharacterStatContributionResponse(
                                    contribution.Source,
                                    contribution.Value))
                                .ToArray()),
                        StringComparer.Ordinal),
                    new CharacterVitalsResponse(
                        snapshot.Character.Vitals.CurrentHp,
                        snapshot.Character.Vitals.MaxHp,
                        snapshot.Character.Vitals.ResourceType,
                        snapshot.Character.Vitals.CurrentResource,
                        snapshot.Character.Vitals.MaxResource,
                        snapshot.Character.Vitals.CheckpointedAtUtc),
                    InventoryEndpoints.ToResponse(snapshot.Character.Inventory)),
            snapshot.World is null
                ? null
                : new BootstrapWorldResponse(
                    ToLocation(snapshot.World.CurrentLocation),
                    snapshot.World.Version,
                    snapshot.World.OutgoingTransitions.Select(ToLocation).ToArray(),
                    snapshot.World.Contracts.Select(contract => new WorldContractResponse(
                        contract.Id,
                        contract.DisplayName,
                        contract.Description,
                        contract.RequiredLevel,
                        contract.TargetMonsterId,
                        contract.UnlockLocationId,
                        contract.Status,
                        contract.OfferLocationId,
                        contract.RewardXp,
                        contract.RewardGold)).ToArray(),
                    snapshot.World.Travel is null
                        ? null
                        : new BootstrapTravelResponse(
                            snapshot.World.Travel.FromLocationId,
                            snapshot.World.Travel.TargetLocationId,
                            snapshot.World.Travel.StartedAtUtc,
                            snapshot.World.Travel.EndsAtUtc)),
            snapshot.ContentVersion,
            snapshot.BalanceVersion,
            snapshot.ServerTimeUtc,
            snapshot.AfkFarm is null
                ? null
                : new BootstrapAfkFarmResponse(
                    snapshot.AfkFarm.SessionId,
                    snapshot.AfkFarm.LocationId,
                    snapshot.AfkFarm.TargetMonsterId,
                    snapshot.AfkFarm.Status.ToString(),
                    snapshot.AfkFarm.StartedAtUtc,
                    snapshot.AfkFarm.EndsAtUtc,
                    snapshot.AfkFarm.ProcessedUntilUtc,
                    snapshot.AfkFarm.CompletedAtUtc,
                    snapshot.AfkFarm.StopReason,
                    snapshot.AfkFarm.Kills,
                    snapshot.AfkFarm.XpEarned,
                    snapshot.AfkFarm.GoldEarned,
                    snapshot.AfkFarm.ItemsCount,
                    snapshot.AfkFarm.EfficiencyPercent),
            snapshot.ReleaseUpdate is null
                ? null
                : new BootstrapReleaseUpdateResponse(
                    snapshot.ReleaseUpdate.Id,
                    snapshot.ReleaseUpdate.Title,
                    snapshot.ReleaseUpdate.PublishedAtUtc,
                    snapshot.ReleaseUpdate.Entries
                        .Select(entry => new ReleaseNoteEntryResponse(
                            entry.Kind.ToString(),
                            entry.Text))
                        .ToArray()));

    private static WorldLocationResponse ToLocation(BootstrapLocation location) =>
        new(
            location.Id,
            location.DisplayName,
            location.DangerLevel,
            location.RecommendedLevel,
            location.MinimumLevel,
            location.MaximumLevel,
            location.RequiredContractId,
            location.ArtId,
            location.Description,
            location.TravelDurationSeconds,
            location.AllowAfk);

    private static WorldLocationResponse ToLocation(
        LocationDefinition location,
        GameContentIndexes indexes)
    {
        WorldLocationResidentResponse[] residents = BuildResidents(location, indexes);
        WorldLocationLootResponse[] loot = BuildLoot(residents, indexes);

        return new WorldLocationResponse(
            location.Id,
            location.DisplayName,
            location.DangerLevel,
            location.RecommendedLevel,
            location.MinimumLevel,
            location.MaximumLevel,
            location.RequiredContractId,
            location.ArtId,
            location.Description,
            location.TravelDurationSeconds,
            location.AllowAfk,
            residents,
            loot);
    }

    private static WorldLocationResidentResponse[] BuildResidents(
        LocationDefinition location,
        GameContentIndexes indexes)
    {
        HashSet<string> monsterIds = new(StringComparer.Ordinal);

        if (location.Encounters is { Count: > 0 })
        {
            foreach (LocationEncounterDefinition encounter in location.Encounters)
                monsterIds.Add(encounter.MonsterId);
        }

        DungeonDefinition? dungeon = indexes.DungeonsById.TryGetValue(location.Id, out DungeonDefinition? directDungeon)
            ? directDungeon
            : indexes.DungeonsById.Values.FirstOrDefault(candidate =>
                string.Equals(candidate.EntryLocationId, location.Id, StringComparison.Ordinal));

        if (dungeon is not null)
        {
            foreach (DungeonEncounterDefinition encounter in dungeon.Encounters)
            {
                monsterIds.Add(encounter.MonsterId);
                if (encounter.Adds is null)
                    continue;

                foreach (DungeonEncounterAddDefinition add in encounter.Adds)
                    monsterIds.Add(add.MonsterId);
            }
        }

        if (monsterIds.Count == 0)
            return [];

        return monsterIds
            .Select(monsterId => indexes.MonstersById.TryGetValue(monsterId, out MonsterDefinition? monster)
                ? monster
                : null)
            .Where(monster => monster is not null)
            .Select(monster => new WorldLocationResidentResponse(
                monster!.Id,
                monster.DisplayName ?? monster.Name,
                monster.Level,
                monster.Rank.ToString(),
                monster.Description,
                monster.ArtId,
                monster.XpReward,
                monster.GoldRewardMin,
                monster.GoldRewardMax))
            .OrderBy(resident => MonsterRankOrder(resident.Rank))
            .ThenBy(resident => resident.Level)
            .ThenBy(resident => resident.DisplayName, StringComparer.Ordinal)
            .ToArray();
    }

    private static WorldLocationLootResponse[] BuildLoot(
        WorldLocationResidentResponse[] residents,
        GameContentIndexes indexes)
    {
        HashSet<string> itemIds = new(StringComparer.Ordinal);

        foreach (WorldLocationResidentResponse resident in residents)
        {
            if (!indexes.MonstersById.TryGetValue(resident.MonsterId, out MonsterDefinition? monster)
                || string.IsNullOrWhiteSpace(monster.LootTableId)
                || !indexes.LootTablesById.TryGetValue(monster.LootTableId, out LootTableDefinition? lootTable))
            {
                continue;
            }

            foreach (LootTableEntry entry in lootTable.Entries)
                itemIds.Add(entry.ItemId);

            if (lootTable.SelectionGroups is null)
                continue;

            foreach (LootSelectionGroup group in lootTable.SelectionGroups)
            {
                foreach (LootSelectionEntry entry in group.Entries)
                    itemIds.Add(entry.ItemId);
            }
        }

        return itemIds
            .Select(itemId => indexes.ItemsById.TryGetValue(itemId, out ItemDefinition? item)
                ? item
                : null)
            .Where(item => item is not null)
            .Select(item => new WorldLocationLootResponse(
                item!.Id,
                item.Name,
                item.Type.ToString(),
                item.Rarity.ToString(),
                item.RequiredLevel,
                item.Description,
                item.IconId))
            .OrderByDescending(item => ItemRarityOrder(item.Rarity))
            .ThenBy(item => item.Type, StringComparer.Ordinal)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static int MonsterRankOrder(string rank) => rank switch
    {
        nameof(MonsterRank.Normal) => 0,
        nameof(MonsterRank.Elite) => 1,
        nameof(MonsterRank.Boss) => 2,
        _ => 3
    };

    private static int ItemRarityOrder(string rarity) => rarity switch
    {
        nameof(ItemRarity.Unique) => 6,
        nameof(ItemRarity.Legendary) => 5,
        nameof(ItemRarity.Epic) => 4,
        nameof(ItemRarity.Rare) => 3,
        nameof(ItemRarity.Uncommon) => 2,
        nameof(ItemRarity.Common) => 1,
        _ => 0
    };

    private static bool TryGetAccountId(
        ClaimsPrincipal user,
        out Guid accountId) =>
        Guid.TryParse(
            user.FindFirstValue(JwtRegisteredClaimNames.Sub),
            out accountId)
        && accountId != Guid.Empty;
}
