using Elyndor.Infrastructure.Administration;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Identity;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.World;
using Elyndor.Infrastructure.Talents;
using Elyndor.Infrastructure.Combat;
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
        builder.Services.AddScoped<BootstrapService>();
        builder.Services.AddScoped<TravelService>();
        builder.Services.AddScoped<TelegramAdministrationService>();
        builder.Services.AddScoped<TalentService>();
        builder.Services.AddScoped<CombatSessionFactory>();
        builder.Services.AddScoped<CombatApplicationService>();
        builder.Services.AddSingleton<IGameRandomFactory, SystemGameRandomFactory>();
        builder.Services.AddSingleton<CombatSessionRegistry>();

        return builder;
    }
}
