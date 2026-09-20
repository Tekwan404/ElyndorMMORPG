using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Core.Professions;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Professions;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Professions;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class ProfessionInventoryCapacityTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 7, 30, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CraftIgnoresEquippedItemAndUsesSlotReleasedByIngredients()
    {
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        Guid equippedItemId = Guid.CreateVersion7();

        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.Accounts.Add(new Account(accountId, Random.Shared.NextInt64(1, long.MaxValue), Now));
            setup.Characters.Add(new Character(
                characterId,
                accountId,
                Guid.CreateVersion7(),
                "Crafter",
                $"CRAFTER{characterId:N}"[..16],
                "HUMAN",
                "MALE",
                "WARRIOR",
                Now));
            setup.CharacterLocations.Add(new CharacterLocation(characterId, "STARTER_TOWN", 1, Now));
            setup.CharacterProfessions.Add(new CharacterProfession(characterId, ProfessionIds.Leatherworking, Now));

            setup.CharacterItems.Add(new CharacterItem(
                Guid.CreateVersion7(),
                characterId,
                "ROUGH_LEATHER",
                4,
                Now));

            for (int index = 0; index < InventoryCapacity.DefaultCapacity - 2; index++)
            {
                setup.CharacterItems.Add(new CharacterItem(
                    Guid.CreateVersion7(),
                    characterId,
                    "ARCHER_COMMON_WHISPER_TRACKER_HANDS",
                    1,
                    Now.AddTicks(index + 1)));
            }

            setup.CharacterItems.Add(new CharacterItem(
                equippedItemId,
                characterId,
                "ARCHER_COMMON_WHISPER_TRACKER_HANDS",
                1,
                Now.AddSeconds(1)));
            setup.CharacterEquipment.Add(new CharacterEquipment(
                characterId,
                EquipmentSlot.Hands,
                equippedItemId));

            await setup.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        ProfessionService service = new(
            context,
            new StaticContentSnapshotProvider(content),
            new FixedTimeProvider(Now));

        ProfessionMutationResult result = await service.CraftAsync(
            accountId,
            "LW_WHISPER_TRACKER_FEET",
            Guid.CreateVersion7(),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorCode);
        Assert.Equal("ARCHER_COMMON_WHISPER_TRACKER_FEET", result.ItemId);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(
            InventoryCapacity.DefaultCapacity - 1,
            await InventoryCapacity.CountUsedSlotsAsync(
                verify,
                characterId,
                CancellationToken.None));
        Assert.Equal(
            1,
            await verify.CharacterItems.CountAsync(item =>
                item.CharacterId == characterId
                && item.ItemDefinitionId == "ARCHER_COMMON_WHISPER_TRACKER_FEET"));
        Assert.True(await verify.CharacterEquipment.AnyAsync(item =>
            item.CharacterId == characterId
            && item.CharacterItemId == equippedItemId));
        Assert.Equal(InventoryCapacity.DefaultCapacity, InventoryCapacity.Resolve(content));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
