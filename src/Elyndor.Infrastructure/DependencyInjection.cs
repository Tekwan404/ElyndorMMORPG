using Elyndor.Infrastructure.Administration;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Identity;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.World;
using Elyndor.Infrastructure.Talents;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Progression;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Social;
using Elyndor.Infrastructure.Parties;
using Elyndor.Infrastructure.Dungeons;
using Elyndor.Infrastructure.Quests;
using Elyndor.Infrastructure.Economy;
using Elyndor.Core.Combat.Randomness;
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
        builder.Services.AddScoped<CombatRewardService>();
        builder.Services.AddScoped<CombatLootRollService>();
        builder.Services.AddHostedService<CombatLootRollExpiryWorker>();
        builder.Services.AddScoped<InventoryEquipmentService>();
        builder.Services.AddScoped<ItemReforgeService>();
        builder.Services.AddScoped<ItemSalvageService>();
        builder.Services.AddScoped<ItemStarUpgradeService>();
        builder.Services.AddScoped<CrystalWalletService>();
        builder.Services.AddScoped<PremiumStoreService>();
        builder.Services.AddScoped<MerchantService>();
        builder.Services.AddScoped<ContentRevisionStore>();
        builder.Services.AddScoped<ContentRevisionImporter>();
        builder.Services.AddScoped<ContentPublicationService>();
        builder.Services.AddScoped<ContentAdministrationService>();
        builder.Services.AddScoped<FriendService>();
        builder.Services.AddScoped<PartyService>();
        builder.Services.AddScoped<DungeonService>();
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
