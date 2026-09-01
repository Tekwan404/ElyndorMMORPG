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

IResourceBuilder<PostgresServerResource> postgres = builder
    .AddPostgres("postgres")
    .WithImageTag("18.4")
    .WithDataVolume();

IResourceBuilder<PostgresDatabaseResource> gameDatabase = postgres.AddDatabase("game");

IResourceBuilder<ProjectResource> server = builder
    .AddProject<Projects.Elyndor_Server>("server")
    .WithReference(gameDatabase)
    .WaitFor(gameDatabase)
    .WithEnvironment("Database__MigrateOnStartup", "true")
    .WithHttpHealthCheck("/health");

if (publicTest)
{
    server
        .WithEnvironment("ASPNETCORE_ENVIRONMENT", "PublicTest")
        .WithEnvironment("Authentication__Development__Enabled", "false")
        .WithEnvironment("Administration__Telegram__Enabled", "true")
        .WithEnvironment(
            "Administration__Telegram__WebhookSecret",
            builder.Configuration["Administration:Telegram:WebhookSecret"] ?? string.Empty)
        .WithEnvironment(
            "Administration__Telegram__AllowedUserIds__0",
            builder.Configuration["Administration:Telegram:AllowedUserIds:0"] ?? string.Empty);
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
