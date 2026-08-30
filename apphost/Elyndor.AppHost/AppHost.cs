IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);
bool publicTest = bool.TryParse(
    builder.Configuration["Elyndor:PublicTest"],
    out bool configuredPublicTest)
    && configuredPublicTest;

IResourceBuilder<PostgresServerResource> postgres = builder
    .AddPostgres("postgres")
    .WithImageTag("18.4")
    .WithDataVolume();

IResourceBuilder<PostgresDatabaseResource> gameDatabase = postgres.AddDatabase("game");

IResourceBuilder<ProjectResource> server = builder
    .AddProject<Projects.Elyndor_Server>("server")
    .WithReference(gameDatabase)
    .WaitFor(gameDatabase)
    .WithHttpHealthCheck("/health");

if (publicTest)
{
    server
        .WithEnvironment("ASPNETCORE_ENVIRONMENT", "PublicTest")
        .WithEnvironment("Authentication__Development__Enabled", "false");
}

if (!publicTest)
{
    builder
        .AddViteApp("web", "../../web/elyndor-web")
        .WithReference(server)
        .WaitFor(server)
        .WithExternalHttpEndpoints();
}

builder.Build().Run();
