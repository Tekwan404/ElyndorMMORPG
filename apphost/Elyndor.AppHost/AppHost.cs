IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);
bool publicTest = bool.TryParse(
    builder.Configuration["Elyndor:PublicTest"],
    out bool configuredPublicTest)
    && configuredPublicTest;
long developmentTelegramUserId = long.TryParse(
    builder.Configuration["Elyndor:DevelopmentTelegramUserId"],
    out long configuredDevelopmentTelegramUserId)
    ? configuredDevelopmentTelegramUserId
    : 1_000_001;

long[] telegramAdminUserIds = builder.Configuration
    .GetSection("Administration:Telegram:AllowedUserIds")
    .GetChildren()
    .Select(item =>
        long.TryParse(
            item.Value,
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out long telegramUserId)
            ? telegramUserId
            : 0)
    .Where(telegramUserId => telegramUserId > 0)
    .Distinct()
    .ToArray();

IResourceBuilder<PostgresServerResource> postgres = builder
    .AddPostgres("postgres")
    .WithImageTag("18.4")
    .WithDataVolume();

IResourceBuilder<PostgresDatabaseResource> gameDatabase = postgres.AddDatabase("game");

const string postgresStabilityHealthCheckName = "game-postgres-stability";
builder.Services
    .AddHealthChecks()
    .AddCheck(
        postgresStabilityHealthCheckName,
        new PostgresStabilityHealthCheck(
            cancellationToken =>
                gameDatabase.Resource.GetConnectionStringAsync(cancellationToken)));
gameDatabase.WithHealthCheck(postgresStabilityHealthCheckName);

IResourceBuilder<ProjectResource> server = builder
    .AddProject<Projects.Elyndor_Server>("server")
    .WithReference(gameDatabase)
    .WaitFor(gameDatabase)
    .WithEnvironment("Database__MigrateOnStartup", "true")
    .WithEnvironment("Content__AllowFileFallbackOnRestoreFailure", "true")
    .WithHttpHealthCheck("/health");

if (publicTest)
{
    server
        .WithEnvironment("ASPNETCORE_ENVIRONMENT", "PublicTest")
        .WithEnvironment("Authentication__Development__Enabled", "false")
        .WithEnvironment("Administration__Telegram__Enabled", "true")
        .WithEnvironment(
            "Administration__Telegram__WebhookSecret",
            builder.Configuration["Administration:Telegram:WebhookSecret"] ?? string.Empty);

    for (int index = 0; index < telegramAdminUserIds.Length; index++)
    {
        server.WithEnvironment(
            $"Administration__Telegram__AllowedUserIds__{index}",
            telegramAdminUserIds[index].ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}

if (!publicTest)
{
    server
        .WithEnvironment(
            "Authentication__SigningKey",
            "elyndor-local-development-only-signing-key-2026")
        .WithEnvironment("Authentication__Telegram__BotToken", "development-only:no-telegram")
        .WithEnvironment("Authentication__Development__Enabled", "true")
        .WithEnvironment(
            "Authentication__Development__TelegramUserId",
            developmentTelegramUserId.ToString(System.Globalization.CultureInfo.InvariantCulture));

    builder
        .AddViteApp("web", "../../web/elyndor-web")
        .WithReference(server)
        .WaitFor(server)
        .WithExternalHttpEndpoints();
}

builder.Build().Run();
