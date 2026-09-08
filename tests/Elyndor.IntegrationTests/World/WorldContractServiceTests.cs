using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.Quests;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.World;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.World;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class WorldContractServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 6, 22, 10, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task BroodmotherContractMustBeAcceptedAtItsOfferLocation()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync(
            level: 14,
            locationId: "DEEP_FOREST");
        await using GameDbContext context = postgres.CreateDbContext();
        WorldContractService service = await CreateServiceAsync(context);

        WorldContractAcceptResult blocked = await service.AcceptAsync(
            accountId,
            "CONTRACT_BROODMOTHER_GATE",
            CancellationToken.None);

        Assert.False(blocked.IsSuccess);
        Assert.Equal(WorldContractErrorCodes.InvalidLocation, blocked.ErrorCode);
        Assert.Empty(await context.CharacterContractAcceptances.AsNoTracking().ToArrayAsync());

        CharacterLocation location = await context.CharacterLocations
            .SingleAsync(candidate => candidate.CharacterId == characterId);
        location.Relocate("BROODMOTHER_LAIR", Now);
        await context.SaveChangesAsync();
        await CompleteBroodmotherPrerequisiteAsync(context, characterId);

        WorldContractAcceptResult accepted = await service.AcceptAsync(
            accountId,
            "CONTRACT_BROODMOTHER_GATE",
            CancellationToken.None);

        Assert.True(accepted.IsSuccess);
        CharacterContractAcceptance persisted = await context.CharacterContractAcceptances
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal(characterId, persisted.CharacterId);
        Assert.Equal("CONTRACT_BROODMOTHER_GATE", persisted.ContractId);
    }

    [Fact]
    public async Task AcceptingSameContractTwiceIsIdempotent()
    {
        (Guid accountId, _) = await CreateCharacterAsync(
            level: 14,
            locationId: "BROODMOTHER_LAIR");
        await using GameDbContext context = postgres.CreateDbContext();
        await CompleteBroodmotherPrerequisiteAsync(
            context,
            await context.Characters
                .Where(character => character.AccountId == accountId)
                .Select(character => character.Id)
                .SingleAsync());
        WorldContractService service = await CreateServiceAsync(context);

        Assert.True((await service.AcceptAsync(
            accountId,
            "CONTRACT_BROODMOTHER_GATE",
            CancellationToken.None)).IsSuccess);
        Assert.True((await service.AcceptAsync(
            accountId,
            "CONTRACT_BROODMOTHER_GATE",
            CancellationToken.None)).IsSuccess);

        Assert.Equal(1, await context.CharacterContractAcceptances.CountAsync());
    }

    [Fact]
    public async Task BroodmotherContractCannotBypassQuestChain()
    {
        (Guid accountId, _) = await CreateCharacterAsync(
            level: 14,
            locationId: "BROODMOTHER_LAIR");
        await using GameDbContext context = postgres.CreateDbContext();
        WorldContractService service = await CreateServiceAsync(context);

        WorldContractAcceptResult blocked = await service.AcceptAsync(
            accountId,
            "CONTRACT_BROODMOTHER_GATE",
            CancellationToken.None);

        Assert.False(blocked.IsSuccess);
        Assert.Equal(
            WorldContractErrorCodes.PrerequisiteRequired,
            blocked.ErrorCode);
        Assert.Empty(await context.CharacterContractAcceptances
            .AsNoTracking()
            .ToArrayAsync());
        Assert.Empty(await context.CharacterQuestStates
            .AsNoTracking()
            .ToArrayAsync());
    }

    private static async Task CompleteBroodmotherPrerequisiteAsync(
        GameDbContext context,
        Guid characterId)
    {
        context.QuestRewardGrants.Add(new QuestRewardGrant(
            characterId,
            "QUEST_13_BROODMOTHER_TRACE",
            Guid.CreateVersion7(),
            0,
            0,
            "[]",
            Now));
        await context.SaveChangesAsync();
    }

    private async Task<(Guid AccountId, Guid CharacterId)> CreateCharacterAsync(
        int level,
        string locationId)
    {
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.Add(new Account(
            accountId,
            Random.Shared.NextInt64(1, long.MaxValue),
            Now));
        Character character = new(
            characterId,
            accountId,
            Guid.CreateVersion7(),
            "Contractor",
            $"CONTRACT{characterId:N}"[..16],
            "HUMAN",
            "MALE",
            "WARRIOR",
            Now);
        character.SetLevel(level);
        context.Characters.Add(character);
        context.CharacterLocations.Add(
            new CharacterLocation(characterId, locationId, 1, Now));
        await context.SaveChangesAsync();
        return (accountId, characterId);
    }

    private static async Task<WorldContractService> CreateServiceAsync(
        GameDbContext context)
    {
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        return new WorldContractService(
            context,
            new StaticContentSnapshotProvider(content),
            new FixedTimeProvider(Now));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
