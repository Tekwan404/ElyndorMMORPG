using System.Text;
using Elyndor.Contracts.System;
using Elyndor.Core.Content;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Administration;
using Elyndor.Infrastructure.Identity.Telegram;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Server.Characters;
using Elyndor.Server.Administration;
using Elyndor.Server.Identity;
using Elyndor.Server.World;
using Elyndor.Server.Talents;
using Elyndor.Server.Combat;
using Elyndor.Server.Items;
using Elyndor.Infrastructure.Combat;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string contentPackagePath = builder.Configuration["Content:PackagePath"]
    ?? Path.Combine(AppContext.BaseDirectory, "content", "package.json");
GameContentPackage gameContentPackage =
    await GameContentPackageLoader.LoadAsync(contentPackagePath);

string frontendDistPath = Path.GetFullPath(
    builder.Configuration["Frontend:DistPath"]
        ?? Path.Combine(
            builder.Environment.ContentRootPath,
            "..",
            "..",
            "web",
            "elyndor-web",
            "dist"));
PhysicalFileProvider? frontendFileProvider = File.Exists(
    Path.Combine(frontendDistPath, "index.html"))
        ? new PhysicalFileProvider(frontendDistPath)
        : null;

builder.AddServiceDefaults();
builder.AddElyndorInfrastructure();

builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
MutableContentSnapshotProvider contentSnapshotProvider =
    new(gameContentPackage);
builder.Services.AddSingleton(contentSnapshotProvider);
builder.Services.AddSingleton<IContentSnapshotProvider>(
    services => services.GetRequiredService<MutableContentSnapshotProvider>());
builder.Services.AddSingleton<TelegramInitDataValidator>();
builder.Services.AddSingleton<JwtTokenIssuer>();
builder.Services.AddSingleton(new HttpClient());
builder.Services.AddSingleton<ITelegramMessageSender, TelegramBotMessageSender>();
builder.Services.AddOptions<TelegramAdminOptions>()
    .BindConfiguration(TelegramAdminOptions.SectionName)
    .Validate(options => options.IsConfigured, "Telegram administration configuration is invalid.")
    .ValidateOnStart();
builder.Services.AddOptions<AuthenticationOptions>()
    .BindConfiguration(AuthenticationOptions.SectionName)
    .Validate(
        options => options.IsValid(),
        "Authentication requires issuer, audience, a 32-byte signing key, Telegram Bot Token, valid time limits, and a positive enabled development identity.")
    .ValidateOnStart();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<AuthenticationOptions>, TimeProvider>(
        (jwtOptions, configuredOptions, timeProvider) =>
        {
            AuthenticationOptions options = configuredOptions.Value;
            jwtOptions.MapInboundClaims = false;
            jwtOptions.SaveToken = false;
            jwtOptions.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    string? token = context.Request.Query["access_token"].FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(token)
                        && context.HttpContext.Request.Path.StartsWithSegments("/hubs/combat"))
                    {
                        context.Token = token;
                    }

                    return Task.CompletedTask;
                }
            };
            jwtOptions.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = options.Issuer,
                ValidateAudience = true,
                ValidAudience = options.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(options.SigningKey)),
                ValidateLifetime = true,
                RoleClaimType = AuthenticationClaimTypes.Role,
                ClockSkew = TimeSpan.FromSeconds(
                    AuthenticationOptions.TokenValidationClockSkewSeconds),
                LifetimeValidator = (notBefore, expires, _, parameters) =>
                    ValidateTokenLifetime(notBefore, expires, timeProvider, parameters.ClockSkew)
            };
        });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        AdminAuthorization.PolicyName,
        policy => policy
            .RequireAuthenticatedUser()
            .RequireRole(AdminAuthorization.SuperAdminRole));
});
builder.Services.AddSignalR();
builder.Services.AddSingleton<ICombatUpdatePublisher, SignalRCombatUpdatePublisher>();

WebApplication app = builder.Build();

bool migrateOnStartup =
    app.Configuration.GetValue<bool>("Database:MigrateOnStartup");
bool restorePublishedOnStartup =
    app.Configuration.GetValue<bool?>("Content:RestorePublishedOnStartup")
    ?? migrateOnStartup;
bool allowFileFallbackOnRestoreFailure =
    app.Configuration.GetValue<bool>(
        "Content:AllowFileFallbackOnRestoreFailure");

if (migrateOnStartup || restorePublishedOnStartup)
{
    await using AsyncServiceScope startupScope = app.Services.CreateAsyncScope();

    if (migrateOnStartup)
    {
        GameDbContext dbContext =
            startupScope.ServiceProvider.GetRequiredService<GameDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    if (restorePublishedOnStartup)
    {
        ContentPublicationService contentPublication =
            startupScope.ServiceProvider.GetRequiredService<ContentPublicationService>();
        ContentStartupRestoreResult restoreResult =
            await ContentStartupRestore.RestoreAsync(
                contentPublication,
                allowFileFallbackOnRestoreFailure);

        if (restoreResult.UsedFileFallback)
        {
            ILogger startupLogger = app.Services
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Elyndor.ContentStartup");
            GameContentSnapshot fileSnapshot = contentSnapshotProvider.GetCurrent();

            StartupLogMessages.LogPublishedContentFallback(
                startupLogger,
                fileSnapshot.ContentVersion,
                fileSnapshot.BalanceVersion,
                restoreResult.FileFallbackReason!);
        }
    }
}

if (frontendFileProvider is not null)
{
    app.Lifetime.ApplicationStopped.Register(frontendFileProvider.Dispose);
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = frontendFileProvider
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = frontendFileProvider,
        OnPrepareResponse = context =>
        {
            if (context.Context.Request.Path.StartsWithSegments("/assets"))
            {
                context.Context.Response.Headers.CacheControl =
                    "public,max-age=31536000,immutable";
            }
        }
    });
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

bool mapDevelopmentAuthentication = app.Environment.IsDevelopment()
    && app.Configuration.GetValue<bool>("Authentication:Development:Enabled");
app.MapAuthenticationEndpoints(mapDevelopmentAuthentication);
app.MapCharacterEndpoints();
app.MapWorldEndpoints();
app.MapTalentEndpoints();
app.MapInventoryEndpoints();
app.MapTelegramAdminEndpoints();
app.MapContentAdminEndpoints();
app.MapHub<CombatHub>("/hubs/combat").RequireAuthorization();

app.MapGet(
        "/api/v1/status",
        (TimeProvider timeProvider) => new ApiStatusResponse(
            "Elyndor.Server",
            "ready",
            timeProvider.GetUtcNow()))
    .WithName("GetApiStatus")
    .WithTags("System");

app.MapDefaultEndpoints();
app.Map("/api/{**path}", () => Results.NotFound());
app.Map("/hubs/{**path}", () => Results.NotFound());

if (frontendFileProvider is not null)
{
    app.MapFallbackToFile(
        "index.html",
        new StaticFileOptions
        {
            FileProvider = frontendFileProvider
        });
}

app.Run();

static bool ValidateTokenLifetime(
    DateTime? notBefore,
    DateTime? expires,
    TimeProvider timeProvider,
    TimeSpan clockSkew)
{
    DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;

    return expires.HasValue
        && expires.Value >= utcNow - clockSkew
        && (!notBefore.HasValue || notBefore.Value <= utcNow + clockSkew);
}

public partial class Program;
