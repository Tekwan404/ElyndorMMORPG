using Elyndor.Infrastructure.Administration;
using Elyndor.Infrastructure.Afk;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Identity;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.World;
using Elyndor.Infrastructure.Talents;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Progression;
using Elyndor.Infrastructure.Raids;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Social;
using Elyndor.Infrastructure.Parties;
using Elyndor.Infrastructure.Dungeons;
using Elyndor.Infrastructure.Quests;
using Elyndor.Infrastructure.Economy;
using Elyndor.Infrastructure.Professions;
using Elyndor.Infrastructure.Pvp;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Pvp;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddElyndorInfrastructure(
        this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<GameDbContext>("game");
        builder.Services.AddScoped<AccountResolver>();
        builder.Services.AddScoped<CharacterCreationService>();
        builder.Services.AddScoped<CharacterCompanionService>();
        builder.Services.AddScoped<CharacterDerivedStateService>();
        builder.Services.AddScoped<BootstrapService>();
        builder.Services.AddScoped<TravelService>();
        builder.Services.AddScoped<WorldContractService>();
        builder.Services.AddScoped<QuestService>();
        builder.Services.AddScoped<WorldEncounterService>();
        builder.Services.AddScoped<TelegramAdministrationService>();
        builder.Services.AddScoped<TalentService>();
        builder.Services.AddScoped<CharacterAbilityCooldownStore>();
        builder.Services.AddScoped<CombatSessionFactory>();
        builder.Services.AddScoped<CombatDurabilityService>();
        builder.Services.AddScoped<CombatApplicationService>();
        builder.Services.AddSingleton<ArenaTestRegistry>(services =>
            new ArenaTestRegistry(services.GetRequiredService<TimeProvider>(),
                services.GetRequiredService<IGameRandomFactory>().Create));
        builder.Services.AddScoped<ArenaTestService>();
        builder.Services.AddOptions<ArenaTestOptions>().BindConfiguration("ArenaTest");
        builder.Services.AddScoped<CombatRewardService>();
        builder.Services.AddScoped<CombatLootRollService>();
        builder.Services.AddHostedService<CombatLootRollExpiryWorker>();
        builder.Services.AddScoped<ArenaQueueService>();
        builder.Services.AddScoped<ArenaMatchmakingService>();
        builder.Services.AddScoped<ArenaSettlementService>();
        builder.Services.AddHostedService<ArenaMatchmakingWorker>();
        builder.Services.AddScoped<InventoryEquipmentService>();
        builder.Services.AddScoped<SpatialInventoryService>();
        builder.Services.AddScoped<ItemReforgeService>();
        builder.Services.AddScoped<ItemSalvageService>();
        builder.Services.AddScoped<ItemEnhancementService>();
#pragma warning disable CS0618 // Legacy /star-upgrade client compatibility.
        builder.Services.AddScoped<ItemStarUpgradeService>();
#pragma warning restore CS0618
        builder.Services.AddScoped<CrystalWalletService>();
        builder.Services.AddScoped<PremiumStoreService>();
        builder.Services.AddScoped<CharacterSkinService>();
        builder.Services.AddScoped<PromoCodeService>();
        builder.Services.AddScoped<MerchantService>();
        builder.Services.AddScoped<CommerceTransaction>();
        builder.Services.AddScoped<PlayerTradeService>();
        builder.Services.AddScoped<AuctionSettlementService>();
        builder.Services.AddOptions<AuctionOptions>().BindConfiguration("Auction")
            .Validate(x => x.ListingFeeRate >= 0 && x.ListingFeeRate <= 1 && x.SaleTaxRate >= 0 && x.SaleTaxRate <= 1 && x.MinimumListingFee >= 0 && x.MaxActiveListings > 0)
            .ValidateOnStart();
        builder.Services.AddHostedService<CommerceExpiryWorker>();
        builder.Services.AddScoped<ProfessionService>();
        builder.Services.AddScoped<ProfessionCorpseService>();
        builder.Services.AddScoped<ContentRevisionStore>();
        builder.Services.AddScoped<ContentRevisionImporter>();
        builder.Services.AddScoped<ContentPublicationService>();
        builder.Services.AddScoped<ContentAdministrationService>();
        builder.Services.AddScoped<FriendService>();
        builder.Services.AddScoped<PartyService>();
        builder.Services.AddScoped<RaidService>();
        builder.Services.AddScoped<DungeonService>();
        builder.Services.AddScoped<AfkFarmService>();
        builder.Services.AddScoped<AfkFarmProgressService>();
        builder.Services.AddHostedService<AfkFarmProgressWorker>();
        builder.Services.AddScoped<DungeonNavigationService>();
        builder.Services.AddScoped<PartyDungeonRunCoordinator>();
        builder.Services.AddSingleton<ContentPublicationCoordinator>();
        builder.Services.AddSingleton<IGameRandomFactory, SystemGameRandomFactory>();
        builder.Services.AddSingleton<WorldEncounterRegistry>();
        builder.Services.AddSingleton<ICombatSessionFinalizer, CombatSessionFinalizer>();
        builder.Services.AddSingleton<CombatSessionRegistry>();
        builder.Services.AddSingleton<ICombatActivityReader>(
            services => services.GetRequiredService<CombatSessionRegistry>());
        builder.Services.AddSingleton<CharacterOperationGuard>();

        return builder;
    }
}