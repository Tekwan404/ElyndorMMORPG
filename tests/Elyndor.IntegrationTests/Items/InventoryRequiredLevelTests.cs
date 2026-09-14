using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Items;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class InventoryRequiredLevelTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 9, 55, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task EquipRejectsItemAboveCharacterLevelBeforeAnyEquipmentMutation()
    {
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        ItemDefinition source = content.Items!.Single(item => item.Id == "RECRUIT_IRON_SWORD");
        ItemDefinition gated = source with
        {
            Id = "TEST_REQUIRED_LEVEL_SWORD",
            Name = "Test Required Level Sword",
            RequiredLevel = 20
        };
        content = content with { Items = content.Items.Concat([gated]).ToArray() };

        (Guid accountId, Guid characterId) = await CreateCharacterAsync(level: 10);
        Guid itemId = await AddItemAsync(characterId, gated.Id);

        await using GameDbContext context = postgres.CreateDbContext();
        InventoryEquipmentService service = new(context, content, new FixedTimeProvider(Now));

        InventoryOperationResult result = await service.EquipAsync(
            accountId,
            itemId,
            Guid.CreateVersion7(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(InventoryErrorCodes.RequiredLevel, result.ErrorCode);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Empty(await verify.CharacterEquipment
            .AsNoTracking()
            .Where(entry => entry.CharacterId == characterId)
            .ToArrayAsync());
        Assert.Equal(0, await verify.CharacterMutations
            .AsNoTracking()
            .CountAsync(entry => entry.CharacterId == characterId));
    }

    private async Task<(Guid AccountId, Guid CharacterId)> CreateCharacterAsync(int level)
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
            "RequiredLevel",
            $"REQ{Guid.NewGuid():N}"[..16],
            "HUMAN",
            "MALE",
            "WARRIOR",
            Now);
        character.SetLevel(level);
        context.Characters.Add(character);
        context.CharacterVitals.Add(new CharacterVitals(characterId, 100, 0, Now, Now));
        await context.SaveChangesAsync();
        return (accountId, characterId);
    }

    private async Task<Guid> AddItemAsync(Guid characterId, string definitionId)
    {
        Guid itemId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        context.CharacterItems.Add(new CharacterItem(
            itemId,
            characterId,
            definitionId,
            1,
            Now));
        await context.SaveChangesAsync();
        return itemId;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
