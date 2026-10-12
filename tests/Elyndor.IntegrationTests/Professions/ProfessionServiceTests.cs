using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.Professions;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Professions;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Professions;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class ProfessionServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 11, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task WorkshopCraftConsumesOnceAndReturnsCanonicalRecipeMetadata()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync();
        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            var profession = new CharacterProfession(characterId, ProfessionIds.Leatherworking, Now);
            for (int skill = 1; skill < 31; skill++) profession.TryIncreaseSkill(300, Now);
            setup.CharacterProfessions.Add(profession);
            setup.CharacterLocations.Add(new Elyndor.Core.World.CharacterLocation(characterId, "STARTER_TOWN", 1, Now));
            setup.CharacterItems.Add(new Elyndor.Core.Items.CharacterItem(Guid.CreateVersion7(), characterId, "THICK_LEATHER", 4, Now));
            await setup.SaveChangesAsync();
        }
        await using GameDbContext context = postgres.CreateDbContext();
        var service = await CreateServiceAsync(context);
        var state = await service.GetStateAsync(accountId, CancellationToken.None);
        Assert.NotNull(state);
        var recipe = Assert.Single(state.Recipes, recipe => recipe.Id == "LW_THICK_FEET");
        Assert.Equal(61, recipe.SkillUpUntil);
        Assert.Equal(12, recipe.OutputRequiredLevel);
        Assert.Equal("Сапоги плотной кожи", recipe.OutputName);
        Assert.NotEmpty(state.MaterialSources!);
        Guid mutationId = Guid.CreateVersion7();
        var result = await service.CraftAsync(accountId, recipe.Id, mutationId, CancellationToken.None);
        var replay = await service.CraftAsync(accountId, recipe.Id, mutationId, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.True(replay.Replayed);
        await using var verify = postgres.CreateDbContext();
        Assert.Equal(1, await verify.CharacterItems.CountAsync(item => item.CharacterId == characterId && item.ItemDefinitionId == recipe.OutputItemId));
        var crafted = await verify.CharacterItems.SingleAsync(item => item.CharacterId == characterId && item.ItemDefinitionId == recipe.OutputItemId);
        Assert.NotNull(crafted.SourceOperationId);
        Assert.NotEqual(mutationId, crafted.SourceOperationId);
        Assert.True(crafted.IsProcedurallyGenerated);
        Assert.False(await verify.CharacterItems.AnyAsync(item => item.CharacterId == characterId && item.ItemDefinitionId == "THICK_LEATHER"));
    }

    [Fact]
    public async Task SkinningCompletesWithNpgsqlRetryStrategy()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync();
        Guid combatSessionId = Guid.CreateVersion7();
        Guid enemyActorId = Guid.CreateVersion7();
        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.CharacterProfessions.Add(new CharacterProfession(characterId, ProfessionIds.Skinning, Now));
            setup.SkinnableCorpses.Add(new SkinnableCorpse(
                characterId,
                combatSessionId,
                enemyActorId,
                "FOREST_WOLF_L1",
                Now,
                Now.AddMinutes(30)));
            await setup.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        ProfessionService service = await CreateServiceAsync(context);

        Guid mutationId = Guid.CreateVersion7();
        ProfessionMutationResult result = await service.SkinAsync(
            accountId,
            combatSessionId,
            enemyActorId,
            mutationId,
            CancellationToken.None);
        ProfessionMutationResult replay = await service.SkinAsync(
            accountId,
            combatSessionId,
            enemyActorId,
            mutationId,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("ROUGH_HIDE", result.ItemId);
        Assert.InRange(result.Quantity, 1, 2);
        Assert.True(replay.IsSuccess);
        Assert.True(replay.Replayed);
        Assert.Equal(result.ItemId, replay.ItemId);
        Assert.Equal(result.Quantity, replay.Quantity);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.True(await verify.SkinnableCorpses
            .AnyAsync(corpse => corpse.CharacterId == characterId && corpse.SkinnedAtUtc != null));
    }

    [Fact]
    public async Task MiningReplayAndCooldownSurviveForgettingProfession()
    {
        var (accountId, characterId) = await CreateCharacterAsync();
        await using var context = postgres.CreateDbContext();
        context.CharacterProfessions.Add(new CharacterProfession(characterId, ProfessionIds.Mining, Now));
        context.CharacterLocations.Add(new Elyndor.Core.World.CharacterLocation(characterId, "WHISPERING_FOREST", 1, Now));
        await context.SaveChangesAsync();
        var service = await CreateServiceAsync(context);
        var mutation = Guid.NewGuid();
        Assert.True((await service.GatherAsync(accountId, "COPPER_VEIN", mutation, CancellationToken.None)).IsSuccess);
        Assert.True((await service.GatherAsync(accountId, "COPPER_VEIN", mutation, CancellationToken.None)).Replayed);
        Assert.Equal("profession_gathering_cooldown", (await service.GatherAsync(accountId, "COPPER_VEIN", Guid.NewGuid(), CancellationToken.None)).ErrorCode);
        var location = await context.CharacterLocations.SingleAsync(item => item.CharacterId == characterId);
        context.CharacterLocations.Remove(location);
        context.CharacterLocations.Add(new Elyndor.Core.World.CharacterLocation(characterId, "STARTER_TOWN", 1, Now));
        await context.SaveChangesAsync();
        Assert.True((await service.ForgetAsync(accountId, ProfessionIds.Mining, Guid.NewGuid(), CancellationToken.None)).IsSuccess);
        Assert.True((await service.LearnAsync(accountId, ProfessionIds.Mining, Guid.NewGuid(), CancellationToken.None)).IsSuccess);
        context.CharacterLocations.Remove(await context.CharacterLocations.SingleAsync(item => item.CharacterId == characterId));
        context.CharacterLocations.Add(new Elyndor.Core.World.CharacterLocation(characterId, "WHISPERING_FOREST", 1, Now));
        await context.SaveChangesAsync();
        Assert.Equal("profession_gathering_cooldown", (await service.GatherAsync(accountId, "COPPER_VEIN", Guid.NewGuid(), CancellationToken.None)).ErrorCode);
        await using var verify = postgres.CreateDbContext();
        Assert.Equal(2, await verify.CharacterItems.Where(item => item.CharacterId == characterId && item.ItemDefinitionId == "COPPER_ORE").SumAsync(item => item.Quantity));
    }

    [Fact]
    public async Task ParallelMiningRequestsGrantOnlyOneYield()
    {
        var (accountId, characterId) = await CreateCharacterAsync();
        await using (var setup = postgres.CreateDbContext())
        {
            setup.CharacterProfessions.Add(new CharacterProfession(characterId, ProfessionIds.Mining, Now));
            setup.CharacterLocations.Add(new Elyndor.Core.World.CharacterLocation(characterId, "WHISPERING_FOREST", 1, Now));
            await setup.SaveChangesAsync();
        }
        async Task<ProfessionMutationResult> Gather()
        {
            await using var context = postgres.CreateDbContext();
            return await (await CreateServiceAsync(context)).GatherAsync(accountId, "COPPER_VEIN", Guid.NewGuid(), CancellationToken.None);
        }
        var results = await Task.WhenAll(Gather(), Gather());
        Assert.Single(results, result => result.IsSuccess);
        Assert.Single(results, result => result.ErrorCode == "profession_gathering_cooldown");
        await using var verify = postgres.CreateDbContext();
        Assert.Equal(2, await verify.CharacterItems.Where(item => item.CharacterId == characterId && item.ItemDefinitionId == "COPPER_ORE").SumAsync(item => item.Quantity));
    }

    [Fact]
    public async Task DisenchantProtectsLockedItemsAndConsumesOnlyOnce()
    {
        var (accountId, characterId) = await CreateCharacterAsync();
        var itemId = Guid.NewGuid();
        await using var context = postgres.CreateDbContext();
        context.CharacterProfessions.Add(new CharacterProfession(characterId, ProfessionIds.Enchanting, Now));
        context.CharacterLocations.Add(new Elyndor.Core.World.CharacterLocation(characterId, "STARTER_TOWN", 1, Now));
        var item = new Elyndor.Core.Items.CharacterItem(itemId, characterId, "RECRUIT_HEAVY_CHEST", 1, Now);
        item.SetLocked(true);
        context.CharacterItems.Add(item);
        await context.SaveChangesAsync();
        var service = await CreateServiceAsync(context);
        Assert.Equal("profession_item_protected", (await service.DisenchantAsync(accountId, itemId, Guid.NewGuid(), CancellationToken.None)).ErrorCode);
        Assert.Equal("profession_enchantment_not_found", (await service.EnchantAsync(accountId, itemId, null!, Guid.NewGuid(), CancellationToken.None)).ErrorCode);
        Assert.Equal("profession_enchantment_not_found", (await service.EnchantAsync(accountId, itemId, "", Guid.NewGuid(), CancellationToken.None)).ErrorCode);
        var unlock = await context.CharacterItems.SingleAsync(item => item.Id == itemId);
        unlock.SetLocked(false);
        await context.SaveChangesAsync();
        var mutation = Guid.NewGuid();
        Assert.True((await service.DisenchantAsync(accountId, itemId, mutation, CancellationToken.None)).IsSuccess);
        Assert.True((await service.DisenchantAsync(accountId, itemId, mutation, CancellationToken.None)).Replayed);
        Assert.Equal("profession_item_not_found", (await service.DisenchantAsync(accountId, itemId, Guid.NewGuid(), CancellationToken.None)).ErrorCode);
        Assert.Equal(ProfessionErrorCodes.IdempotencyConflict, (await service.EnchantAsync(accountId, itemId, "MINOR_STAMINA", mutation, CancellationToken.None)).ErrorCode);
        await using var verify = postgres.CreateDbContext();
        Assert.False(await verify.CharacterItems.AnyAsync(item => item.Id == itemId));
        Assert.Equal(1, await verify.CharacterItems.Where(item => item.CharacterId == characterId && item.ItemDefinitionId == "STRANGE_DUST").SumAsync(item => item.Quantity));
    }

    [Fact]
    public async Task EnchantmentPersistsWithoutStackingOrRerollingAndConsumesScrollOnce()
    {
        var (accountId, characterId) = await CreateCharacterAsync();
        var itemId = Guid.NewGuid();
        await using var context = postgres.CreateDbContext();
        context.CharacterProfessions.Add(new CharacterProfession(characterId, ProfessionIds.Enchanting, Now));
        context.CharacterLocations.Add(new Elyndor.Core.World.CharacterLocation(characterId, "STARTER_TOWN", 1, Now));
        var rolled = new PrimaryStats(4, 0, 0, 2);
        context.CharacterItems.Add(new Elyndor.Core.Items.CharacterItem(itemId, characterId, "RECRUIT_HEAVY_CHEST", 1, Now, rolledPrimaryStats: rolled));
        context.CharacterItems.Add(new Elyndor.Core.Items.CharacterItem(Guid.NewGuid(), characterId, "MINOR_STAMINA_SCROLL", 2, Now));
        context.CharacterItems.Add(new Elyndor.Core.Items.CharacterItem(Guid.NewGuid(), characterId, "MINOR_STRENGTH_SCROLL", 1, Now));
        await context.SaveChangesAsync();
        var service = await CreateServiceAsync(context);
        var mutation = Guid.NewGuid();
        Assert.True((await service.EnchantAsync(accountId, itemId, "MINOR_STAMINA", mutation, CancellationToken.None)).IsSuccess);
        Assert.True((await service.EnchantAsync(accountId, itemId, "MINOR_STAMINA", mutation, CancellationToken.None)).Replayed);
        Assert.True((await service.EnchantAsync(accountId, itemId, "MINOR_STRENGTH", Guid.NewGuid(), CancellationToken.None)).IsSuccess);
        await using var verify = postgres.CreateDbContext();
        var target = await verify.CharacterItems.SingleAsync(item => item.Id == itemId);
        Assert.Equal(rolled, target.RolledPrimaryStats);
        Assert.Equal("MINOR_STRENGTH", target.EnchantmentId);
        Assert.Equal(1, await verify.CharacterItems.Where(item => item.CharacterId == characterId && item.ItemDefinitionId == "MINOR_STAMINA_SCROLL").SumAsync(item => item.Quantity));
        var content = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        var inventory = await new Elyndor.Infrastructure.Items.InventoryEquipmentService(verify, new StaticContentSnapshotProvider(content), new FixedTimeProvider(Now)).GetForCharacterAsync(characterId, CancellationToken.None);
        Assert.Equal(new PrimaryStats(5, 0, 0, 2), inventory.Items.Single(item => item.Id == itemId).EffectiveStats);
    }

    [Fact]
    public async Task WorkshopRejectsCraftingDuringActiveTravelWithoutConsumingMaterials()
    {
        var (accountId, characterId) = await CreateCharacterAsync();
        await using var context = postgres.CreateDbContext();
        context.CharacterProfessions.Add(new CharacterProfession(characterId, ProfessionIds.Mining, Now));
        context.CharacterLocations.Add(new Elyndor.Core.World.CharacterLocation(characterId, "STARTER_TOWN", 1, Now));
        context.CharacterTravelStates.Add(new Elyndor.Core.World.CharacterTravelState(characterId, Guid.NewGuid(), "STARTER_TOWN", "WHISPERING_FOREST", Now, Now.AddMinutes(1)));
        context.CharacterItems.Add(new Elyndor.Core.Items.CharacterItem(Guid.NewGuid(), characterId, "COPPER_ORE", 2, Now));
        await context.SaveChangesAsync();
        var service = await CreateServiceAsync(context);
        Assert.Equal(ProfessionErrorCodes.WrongWorkshop, (await service.CraftAsync(accountId, "MINING_SMELT_COPPER", Guid.NewGuid(), CancellationToken.None)).ErrorCode);
        await using var verify = postgres.CreateDbContext();
        Assert.Equal(2, await verify.CharacterItems.Where(item => item.CharacterId == characterId && item.ItemDefinitionId == "COPPER_ORE").SumAsync(item => item.Quantity));
        Assert.False(await verify.CharacterItems.AnyAsync(item => item.CharacterId == characterId && item.ItemDefinitionId == "COPPER_BAR"));
    }

    [Fact]
    public async Task MiningSkillRollUsesInjectedServerRandomRatherThanClientMutation()
    {
        var (accountId, characterId) = await CreateCharacterAsync();
        await using var context = postgres.CreateDbContext();
        var profession = new CharacterProfession(characterId, ProfessionIds.Mining, Now);
        while (profession.Skill < 15) profession.TryIncreaseSkill(300, Now);
        context.CharacterProfessions.Add(profession);
        context.CharacterLocations.Add(new Elyndor.Core.World.CharacterLocation(characterId, "WHISPERING_FOREST", 1, Now));
        await context.SaveChangesAsync();
        var content = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        var service = new ProfessionService(context, new StaticContentSnapshotProvider(content), new FixedTimeProvider(Now), new FixedRandomFactory());
        var result = await service.GatherAsync(accountId, "COPPER_VEIN", Guid.NewGuid(), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.False(result.SkillIncreased);
        await using var verify = postgres.CreateDbContext();
        Assert.Equal(15, (await verify.CharacterProfessions.SingleAsync(item => item.CharacterId == characterId)).Skill);
    }

    [Fact]
    public async Task MiningRejectsActiveAfkWithoutRewardOrCooldown()
    {
        var (accountId, characterId) = await CreateCharacterAsync();
        await using var context = postgres.CreateDbContext();
        context.CharacterProfessions.Add(new CharacterProfession(characterId, ProfessionIds.Mining, Now));
        context.CharacterLocations.Add(new Elyndor.Core.World.CharacterLocation(characterId, "WHISPERING_FOREST", 1, Now));
        context.AfkFarmSessions.Add(new Elyndor.Core.Afk.AfkFarmSession(Guid.NewGuid(), characterId, "WHISPERING_FOREST", null,
            Now, Now.AddHours(1), "test", "test", "{}", Now));
        await context.SaveChangesAsync();
        var service = await CreateServiceAsync(context);
        var result = await service.GatherAsync(accountId, "COPPER_VEIN", Guid.NewGuid(), CancellationToken.None);
        Assert.Equal("resource_afk_active", result.ErrorCode);
        await using var verify = postgres.CreateDbContext();
        Assert.False(await verify.CharacterItems.AnyAsync(item => item.CharacterId == characterId));
        Assert.False(await verify.CharacterMutations.AnyAsync(item => item.CharacterId == characterId));
        var profession = await verify.CharacterProfessions.SingleAsync(item => item.CharacterId == characterId);
        Assert.Equal(1, profession.Skill);
        Assert.Null(profession.NextGatherAtUtc);
    }

    [Fact]
    public async Task SkinningSkillRollIgnoresClientSelectedWinningMutation()
    {
        var (accountId, characterId) = await CreateCharacterAsync();
        var content = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        var source = content.SkinningSources!.Single(item => item.MonsterId == "FOREST_WOLF_L1");
        await using var context = postgres.CreateDbContext();
        var profession = new CharacterProfession(characterId, ProfessionIds.Skinning, Now);
        while (profession.Skill < source.SkillUpUntil - 1) profession.TryIncreaseSkill(300, Now);
        context.CharacterProfessions.Add(profession);
        var combatId = Guid.NewGuid();
        var enemyId = Guid.NewGuid();
        context.SkinnableCorpses.Add(new SkinnableCorpse(characterId, combatId, enemyId, source.MonsterId, Now, Now.AddMinutes(30)));
        await context.SaveChangesAsync();
        Guid mutation;
        decimal clientRoll;
        do
        {
            mutation = Guid.NewGuid();
            var digest = global::System.Security.Cryptography.SHA256.HashData(global::System.Text.Encoding.UTF8.GetBytes($"{mutation:N}|SKIN_SKILL|{source.Id}"));
            clientRoll = global::System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(digest) / (decimal)uint.MaxValue;
        } while (clientRoll >= 1m / (source.SkillUpUntil - source.RequiredSkill));
        var service = new ProfessionService(context, new StaticContentSnapshotProvider(content), new FixedTimeProvider(Now), new SkinningRandomFactory());
        var result = await service.SkinAsync(accountId, combatId, enemyId, mutation, CancellationToken.None);
        Assert.True(result.IsSuccess, result.ErrorCode);
        Assert.False(result.SkillIncreased);
        var replay = await service.SkinAsync(accountId, combatId, enemyId, mutation, CancellationToken.None);
        Assert.True(replay.Replayed);
        Assert.Equal(result.Quantity, replay.Quantity);
        Assert.False(replay.SkillIncreased);
    }

    [Theory]
    [InlineData("MANA_POTION", "MANA", 120, 16)]
    [InlineData("FOCUS_POTION", "FOCUS", 50, 16)]
    [InlineData("RAGE_POTION", "RAGE", 45, 16)]
    [InlineData("MAJOR_MANA_POTION", "MANA", 160, 31)]
    [InlineData("MAJOR_FOCUS_POTION", "FOCUS", 70, 31)]
    [InlineData("MAJOR_RAGE_POTION", "RAGE", 60, 31)]
    public async Task AlchemyCraftAndUseRestoreResourceOnlyOnce(string potionId, string resourceType, int amount, int skill)
    {
        var (accountId, characterId) = await CreateCharacterAsync(resourceType switch { "MANA" => "MAGE", "FOCUS" => "ARCHER", _ => "WARRIOR" });
        var content = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        var recipe = Assert.Single(content.ProfessionRecipes!, item => item.OutputItemId == potionId);
        Assert.Equal(skill, recipe.RequiredSkill);
        await using var context = postgres.CreateDbContext();
        (await context.Characters.SingleAsync(item => item.Id == characterId)).SetLevel(15);
        var profession = new CharacterProfession(characterId, ProfessionIds.Alchemy, Now);
        while (profession.Skill < skill) profession.TryIncreaseSkill(300, Now);
        context.CharacterProfessions.Add(profession);
        context.CharacterLocations.Add(new Elyndor.Core.World.CharacterLocation(characterId, "STARTER_TOWN", 1, Now));
        context.CharacterVitals.Add(new CharacterVitals(characterId, 200, 10, Now, Now));
        foreach (var ingredient in recipe.Ingredients)
            context.CharacterItems.Add(new Elyndor.Core.Items.CharacterItem(Guid.NewGuid(), characterId, ingredient.ItemId, ingredient.Quantity * 2, Now));
        await context.SaveChangesAsync();
        var service = await CreateServiceAsync(context);
        var craftMutation = Guid.NewGuid();
        Assert.True((await service.CraftAsync(accountId, recipe.Id, craftMutation, CancellationToken.None)).IsSuccess);
        Assert.True((await service.CraftAsync(accountId, recipe.Id, craftMutation, CancellationToken.None)).Replayed);
        await using var useContext = postgres.CreateDbContext();
        var potion = await useContext.CharacterItems.SingleAsync(item => item.CharacterId == characterId && item.ItemDefinitionId == potionId);
        var inventory = new Elyndor.Infrastructure.Items.InventoryEquipmentService(useContext, new StaticContentSnapshotProvider(content), new FixedTimeProvider(Now));
        var useMutation = Guid.NewGuid();
        Assert.True((await inventory.UseConsumableOutOfCombatAsync(accountId, potion.Id, useMutation, 200, resourceType, 500, Now, CancellationToken.None)).IsSuccess);
        Assert.True((await inventory.UseConsumableOutOfCombatAsync(accountId, potion.Id, useMutation, 200, resourceType, 500, Now, CancellationToken.None)).IsSuccess);
        await using var verify = postgres.CreateDbContext();
        Assert.Equal(10 + amount, (await verify.CharacterVitals.SingleAsync(item => item.CharacterId == characterId)).CurrentResource);
        Assert.False(await verify.CharacterItems.AnyAsync(item => item.Id == potion.Id));
        foreach (var ingredient in recipe.Ingredients)
            Assert.Equal(ingredient.Quantity, await verify.CharacterItems.Where(item => item.CharacterId == characterId && item.ItemDefinitionId == ingredient.ItemId).SumAsync(item => item.Quantity));
    }

    [Theory]
    [InlineData("BS_COPPER_HANDS")]
    [InlineData("BS_COPPER_FEET")]
    [InlineData("BS_BRONZE_SWORD")]
    [InlineData("BS_BRONZE_CHEST")]
    [InlineData("BS_BRONZE_HANDS")]
    [InlineData("BS_IRON_SWORD")]
    [InlineData("BS_IRON_HANDS")]
    [InlineData("BS_IRON_FEET")]
    [InlineData("ENCHANT_MINOR_AGILITY")]
    [InlineData("ENCHANT_LESSER_STAMINA")]
    [InlineData("ENCHANT_LESSER_STRENGTH")]
    [InlineData("ENCHANT_LESSER_AGILITY")]
    [InlineData("ENCHANT_LESSER_INTELLECT")]
    public async Task NewWorkshopRecipeProducesUsableOutputAndConsumesOnce(string recipeId)
    {
        var (accountId, characterId) = await CreateCharacterAsync();
        var content = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        var recipe = content.ProfessionRecipes!.Single(item => item.Id == recipeId);
        await using var context = postgres.CreateDbContext();
        var profession = new CharacterProfession(characterId, recipe.ProfessionId, Now);
        while (profession.Skill < recipe.RequiredSkill) profession.TryIncreaseSkill(300, Now);
        context.CharacterProfessions.Add(profession);
        context.CharacterLocations.Add(new Elyndor.Core.World.CharacterLocation(characterId, "STARTER_TOWN", 1, Now));
        foreach (var ingredient in recipe.Ingredients)
            context.CharacterItems.Add(new Elyndor.Core.Items.CharacterItem(Guid.NewGuid(), characterId, ingredient.ItemId, ingredient.Quantity, Now));
        await context.SaveChangesAsync();
        var service = await CreateServiceAsync(context);
        var mutation = Guid.NewGuid();
        Assert.True((await service.CraftAsync(accountId, recipeId, mutation, CancellationToken.None)).IsSuccess);
        Assert.True((await service.CraftAsync(accountId, recipeId, mutation, CancellationToken.None)).Replayed);
        await using var verify = postgres.CreateDbContext();
        var output = await verify.CharacterItems.SingleAsync(item => item.CharacterId == characterId && item.ItemDefinitionId == recipe.OutputItemId);
        foreach (var ingredient in recipe.Ingredients)
            Assert.False(await verify.CharacterItems.AnyAsync(item => item.CharacterId == characterId && item.ItemDefinitionId == ingredient.ItemId));
        if (recipe.ProfessionId == ProfessionIds.Blacksmithing)
        {
            Assert.True(output.IsProcedurallyGenerated);
            Assert.NotEqual(mutation, output.SourceOperationId);
        }
        else
        {
            var targetId = Guid.NewGuid();
            verify.CharacterItems.Add(new Elyndor.Core.Items.CharacterItem(targetId, characterId, "RECRUIT_HEAVY_CHEST", 1, Now));
            await verify.SaveChangesAsync();
            var enchantment = content.ProfessionEnchantments!.Single(item => item.ScrollItemId == recipe.OutputItemId);
            var enchantService = await CreateServiceAsync(verify);
            Assert.True((await enchantService.EnchantAsync(accountId, targetId, enchantment.Id, Guid.NewGuid(), CancellationToken.None)).IsSuccess);
            await using var final = postgres.CreateDbContext();
            Assert.Equal(enchantment.Id, (await final.CharacterItems.SingleAsync(item => item.Id == targetId)).EnchantmentId);
            Assert.False(await final.CharacterItems.AnyAsync(item => item.Id == output.Id));
        }
    }

    [Fact]
    public async Task ParallelCraftRequestsCannotSpendTheSameIngredientsTwice()
    {
        var (accountId, characterId) = await CreateCharacterAsync();
        await using (var setup = postgres.CreateDbContext())
        {
            setup.CharacterProfessions.Add(new CharacterProfession(characterId, ProfessionIds.Blacksmithing, Now));
            setup.CharacterLocations.Add(new Elyndor.Core.World.CharacterLocation(characterId, "STARTER_TOWN", 1, Now));
            setup.CharacterItems.Add(new Elyndor.Core.Items.CharacterItem(Guid.NewGuid(), characterId, "COPPER_BAR", 3, Now));
            await setup.SaveChangesAsync();
        }
        async Task<ProfessionMutationResult> CraftAsync()
        {
            await using var context = postgres.CreateDbContext();
            return await (await CreateServiceAsync(context)).CraftAsync(accountId, "BS_COPPER_HANDS", Guid.NewGuid(), CancellationToken.None);
        }
        var results = await Task.WhenAll(CraftAsync(), CraftAsync());
        Assert.Single(results, result => result.IsSuccess);
        Assert.Single(results, result => result.ErrorCode == ProfessionErrorCodes.MissingIngredients);
        await using var verify = postgres.CreateDbContext();
        Assert.Equal(1, await verify.CharacterItems.CountAsync(item => item.CharacterId == characterId && item.ItemDefinitionId == "CRAFT_COPPER_HANDS"));
        Assert.False(await verify.CharacterItems.AnyAsync(item => item.CharacterId == characterId && item.ItemDefinitionId == "COPPER_BAR"));
    }

    private sealed class SkinningRandomFactory : Elyndor.Core.Combat.Randomness.IGameRandomFactory
    {
        public Elyndor.Core.Combat.Randomness.IGameRandom Create() => new Elyndor.Core.Combat.Randomness.SequenceGameRandom(0.99m, 0.99m);
    }

    private sealed class FixedRandomFactory : Elyndor.Core.Combat.Randomness.IGameRandomFactory
    {
        public Elyndor.Core.Combat.Randomness.IGameRandom Create() => new Elyndor.Core.Combat.Randomness.SequenceGameRandom(0.99m);
    }

    private async Task<(Guid AccountId, Guid CharacterId)> CreateCharacterAsync(string classId = "WARRIOR")
    {
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.Add(new Account(accountId, Random.Shared.NextInt64(1, long.MaxValue), Now));
        context.Characters.Add(new Character(
            characterId,
            accountId,
            Guid.CreateVersion7(),
            "Skinner",
            $"SKINNER{characterId:N}"[..16],
            "HUMAN",
            "MALE",
            classId,
            Now));
        await context.SaveChangesAsync();
        return (accountId, characterId);
    }

    private static async Task<ProfessionService> CreateServiceAsync(GameDbContext context)
    {
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        return new ProfessionService(
            context,
            new StaticContentSnapshotProvider(content),
            new FixedTimeProvider(Now));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
