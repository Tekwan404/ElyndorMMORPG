using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Identity;
using Elyndor.Infrastructure.Persistence;
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

        return builder;
    }
}
