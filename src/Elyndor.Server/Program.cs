using System.Text;
using Elyndor.Contracts.System;
using Elyndor.Core.Content;
using Elyndor.Core.World;
using Elyndor.Core.Releases;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Administration;
using Elyndor.Infrastructure.Identity.Telegram;
using Elyndor.Infrastructure.Releases;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Server;
using Elyndor.Server.Characters;
using Elyndor.Server.Administration;
using Elyndor.Server.Identity;
using Elyndor.Server.World;
using Elyndor.Server.Quests;
using Elyndor.Server.Talents;
using Elyndor.Server.Combat;
using Elyndor.Server.Items;
using Elyndor.Server.Social;
using Elyndor.Server.Parties;
using Elyndor.Server.Raids;
using Elyndor.Server.Dungeons;
using Elyndor.Server.Economy;
using Elyndor.Server.Afk;
using Elyndor.Server.Professions;
using Elyndor.Server.Releases;
using Elyndor.Server.Monitoring;
using Elyndor.Infrastructure.Combat;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string contentPackagePath = builder.Configuration["Content:PackagePath"]
    ?? Path.Combine(AppContext.BaseDirectory, "content", "package.json");
GameContentPackage gameContentPackage =
    await GameContentPackageLoader.LoadAsync(contentPackagePath);
string releaseNotesPath = builder.Configuration["Releases:NotesPath"]
    ?? Path.Combine(Path.GetDirectoryName(contentPackagePath)!, "release-notes.json");
IReleaseNotesCatalog releaseNotesCatalog;
Exception? releaseNotesLoadFailure = null;
try
{
    releaseNotesCatalog = await ReleaseNotesFileLoader.LoadAsync(releaseNotesPath);
}
catch (Exception exception)
{
    releaseNotesLoadFailure = exception;
    releaseNotesCatalog = new ReleaseNotesCatalog(new ReleaseNotesDocument([]));
}

string frontendDistPath = FrontendDistPathResolver.Resolve(
    builder.Configuration["Frontend:DistPath"],
    "frontend",
    Path.Combine(builder.Environment.ContentRootPath, "..", "..", "web", "elyndor-web", "dist"));
PhysicalFileProvider? frontendFileProvider = File.Exists(Path.Combine(frontendDistPath, "index.html"))
    ? new PhysicalFileProvider(frontendDistPath)
    : null;

string adminFrontendDistPath = FrontendDistPathResolver.Resolve(
    builder.Configuration["AdminFrontend:DistPath"],
    "frontend-admin",
    Path.Combine(builder.Environment.ContentRootPath, "..", "..", "web", "elyndor-admin", "dist"));
PhysicalFileProvider? adminFrontendFileProvider = File.Exists(Path.Combine(adminFrontendDistPath, "index.html"))
    ? new PhysicalFileProvider(adminFrontendDistPath)
    : null;

builder.AddServiceDefaults();
builder.AddElyndorInfrastructure();

builder.Services.AddOpenApi();
builder.Services.AddElyndorRateLimiting(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
MutableContentSnapshotProvider contentSnapshotProvider = new(gameContentPackage);
builder.Services.AddSingleton(contentSnapshotProvider);
builder.Services.AddSingleton<IContentSnapshotProvider>(services => services.GetRequiredService<MutableContentSnapshotProvider>());
builder.Services.AddSingleton(releaseNotesCatalog);
builder.Services.AddScoped<ReleaseAcknowledgementService>();
builder.Services.AddScoped<ReleaseAdminNotificationService>();
builder.Services.AddSingleton<TelegramInitDataValidator>();
builder.Services.AddSingleton<JwtTokenIssuer>();
builder.Services.AddSingleton(new HttpClient());
builder.Services.AddSingleton<ITelegramMessageSender, TelegramBotMessageSender>();
builder.Services.AddScoped<TelegramAdminUpdateProcessor>();
builder.Services.AddScoped<TelegramServerErrorReporter>();
builder.Services.AddSingleton<TelegramWebhookRegistrationService>();
builder.Services.AddHostedService<TelegramWebhookRegistrationWorker>();
builder.Services.AddHostedService<TelegramAdminLongPollingWorker>();
builder.Services.AddOptions<TelegramAdminOptions>()
    .BindConfiguration(TelegramAdminOptions.SectionName)
    .Validate(options => options.IsConfigured, "Telegram administration configuration is invalid.")
    .ValidateOnStart();
builder.Services.AddSingleton<IServerMetricsCollector, ServerMetricsCollector>();
builder.Services.AddSingleton<ServerErrorMetrics>();
builder.Services.AddHostedService<TelegramServerMonitoringWorker>();
builder.Services.AddSingleton<AdminWebAuthenticationService>();
builder.Services.AddOptions<AdminWebAuthenticationOptions>()
    .BindConfiguration(AdminWebAuthenticationOptions.SectionName)
    .Validate(options => options.IsConfigured, $"Emergency admin password must be at least {AdminWebAuthenticationOptions.MinimumEmergencyPasswordBytes} UTF-8 bytes when enabled.")
    .ValidateOnStart();
builder.Services.AddOptions<AuthenticationOptions>()
    .BindConfiguration(AuthenticationOptions.SectionName)
    .Validate(options => options.IsValid(), "Authentication requires issuer, audience, a 32-byte signing key, Telegram Bot Token, valid time limits, and a positive enabled development identity.")
    .ValidateOnStart();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<AuthenticationOptions>, TimeProvider>((jwtOptions, configuredOptions, timeProvider) =>
    {
        AuthenticationOptions options = configuredOptions.Value;
        jwtOptions.MapInboundClaims = false;
        jwtOptions.SaveToken = false;
        jwtOptions.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                string? token = context.Request.Query["access_token"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs/combat"))
                    context.Token = token;
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            ValidateLifetime = true,
            RoleClaimType = AuthenticationClaimTypes.Role,
            ClockSkew = TimeSpan.FromSeconds(AuthenticationOptions.TokenValidationClockSkewSeconds),
            LifetimeValidator = (notBefore, expires, _, parameters) => ValidateTokenLifetime(notBefore, expires, timeProvider, parameters.ClockSkew)
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AdminAuthorization.PolicyName, policy => policy.RequireAuthenticatedUser().RequireRole(AdminAuthorization.SuperAdminRole));
});
builder.Services.AddSignalR();
builder.Services.AddSingleton<ICombatUpdatePublisher, SignalRCombatUpdatePublisher>();

WebApplication app = builder.Build();

if (releaseNotesLoadFailure is not null)
    StartupLogMessages.LogReleaseNotesLoadFailed(app.Logger, releaseNotesPath, releaseNotesLoadFailure);

bool migrateOnStartup = app.Configuration.GetValue<bool>("Database:MigrateOnStartup");
bool restorePublishedOnStartup = app.Configuration.GetValue<bool?>("Content:RestorePublishedOnStartup") ?? migrateOnStartup;
bool allowFileFallbackOnRestoreFailure = app.Configuration.GetValue<bool>("Content:AllowFileFallbackOnRestoreFailure");

if (migrateOnStartup || restorePublishedOnStartup)
{
    await using AsyncServiceScope startupScope = app.Services.CreateAsyncScope();
    if (migrateOnStartup)
    {
        GameDbContext dbContext = startupScope.ServiceProvider.GetRequiredService<GameDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    if (restorePublishedOnStartup)
    {
        ContentPublicationService contentPublication = startupScope.ServiceProvider.GetRequiredService<ContentPublicationService>();
        ContentStartupRestoreResult restoreResult = await ContentStartupRestore.RestoreAsync(contentPublication, allowFileFallbackOnRestoreFailure);
        if (restoreResult.UsedFileFallback)
        {
            ILogger startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Elyndor.ContentStartup");
            GameContentSnapshot fileSnapshot = contentSnapshotProvider.GetCurrent();
            Elyndor.Server.StartupLogMessages.LogPublishedContentFallback(startupLogger, fileSnapshot.ContentVersion, fileSnapshot.BalanceVersion, restoreResult.FileFallbackReason!);
        }
    }
}

await using (AsyncServiceScope combatRecoveryScope = app.Services.CreateAsyncScope())
{
    GameDbContext recoveryDbContext = combatRecoveryScope.ServiceProvider.GetRequiredService<GameDbContext>();
    if (await recoveryDbContext.Database.CanConnectAsync())
    {
        CombatDurabilityService durability = combatRecoveryScope.ServiceProvider.GetRequiredService<CombatDurabilityService>();
        await durability.RecoverInterruptedAsync(CancellationToken.None);
    }
}

try
{
    await using AsyncServiceScope releaseNotificationScope = app.Services.CreateAsyncScope();
    ReleaseAdminNotificationService releaseNotifier = releaseNotificationScope.ServiceProvider.GetRequiredService<ReleaseAdminNotificationService>();
    await releaseNotifier.NotifyCurrentReleaseAsync(CancellationToken.None);
}
catch (Exception exception)
{
    StartupLogMessages.LogReleaseAdminNotificationFailed(app.Logger, exception);
}

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        Exception? exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        if (exception is not null)
        {
            ILogger logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Elyndor.UnhandledRequest");
            StartupLogMessages.LogUnhandledRequestException(logger, context.Request.Method, context.Request.Path, context.TraceIdentifier, exception);
            ServerErrorMetrics errorMetrics = context.RequestServices.GetRequiredService<ServerErrorMetrics>();
            errorMetrics.Record(exception, context.Request.Path);
            TelegramServerErrorReporter reporter = context.RequestServices.GetRequiredService<TelegramServerErrorReporter>();
            await reporter.ReportAsync(context, exception, context.RequestAborted);
        }

        await Results.Problem(statusCode: StatusCodes.Status500InternalServerError, extensions: new Dictionary<string, object?>
        {
            ["code"] = "internal_server_error",
            ["correlationId"] = context.TraceIdentifier
        }).ExecuteAsync(context);
    });
});

if (frontendFileProvider is not null)
{
    app.Lifetime.ApplicationStopped.Register(frontendFileProvider.Dispose);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = frontendFileProvider });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = frontendFileProvider,
        OnPrepareResponse = context =>
        {
            if (context.Context.Request.Path.StartsWithSegments("/assets"))
                context.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        }
    });
}

if (adminFrontendFileProvider is not null)
{
    app.Lifetime.ApplicationStopped.Register(adminFrontendFileProvider.Dispose);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = adminFrontendFileProvider,
        RequestPath = "/__admin",
        OnPrepareResponse = context =>
        {
            if (context.Context.Request.Path.StartsWithSegments("/__admin/assets"))
                context.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        }
    });
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

bool mapDevelopmentAuthentication = app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Authentication:Development:Enabled");
app.MapAuthenticationEndpoints(mapDevelopmentAuthentication);
app.MapAdminWebAuthenticationEndpoints();
app.MapCharacterEndpoints();
app.MapWorldEndpoints();
app.MapQuestEndpoints();
app.MapTalentEndpoints();
app.MapInventoryEndpoints();
app.MapSpatialInventoryEndpoints();
app.MapItemStarUpgradePreviewEndpoints();
app.MapEconomyEndpoints();
app.MapProfessionEndpoints();
app.MapAfkFarmEndpoints();
app.MapReleaseNotesEndpoints();
app.MapSocialEndpoints();
app.MapPartyEndpoints();
app.MapRaidEndpoints();
app.MapDungeonEndpoints();
app.MapTelegramAdminEndpoints();
app.MapContentAdminEndpoints();
app.MapBossCombatLogEndpoints();
app.MapBossCombatLogArchiveEndpoints();
app.MapHub<CombatHub>("/hubs/combat").RequireAuthorization();

app.MapGet("/api/v1/status", (TimeProvider timeProvider) => new ApiStatusResponse("Elyndor.Server", "ready", timeProvider.GetUtcNow()))
    .WithName("GetApiStatus")
    .WithTags("System");

app.MapDefaultEndpoints();
app.Map("/api/{**path}", () => Results.NotFound());
app.Map("/hubs/{**path}", () => Results.NotFound());

if (adminFrontendFileProvider is not null)
{
    app.MapFallbackToFile("/__admin/{*path:nonfile}", "index.html", new StaticFileOptions { FileProvider = adminFrontendFileProvider });
}

if (frontendFileProvider is not null)
{
    app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = frontendFileProvider });
}

app.Run();

static bool ValidateTokenLifetime(DateTime? notBefore, DateTime? expires, TimeProvider timeProvider, TimeSpan clockSkew)
{
    DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
    return expires.HasValue && expires.Value >= utcNow - clockSkew && (!notBefore.HasValue || notBefore.Value <= utcNow + clockSkew);
}

public partial class Program;