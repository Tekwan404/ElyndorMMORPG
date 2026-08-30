using Elyndor.Contracts.System;
using Elyndor.Core.Content;
using Elyndor.Infrastructure.Content;
using Microsoft.Extensions.FileProviders;

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
builder.Services.AddSingleton(gameContentPackage);

WebApplication app = builder.Build();

if (frontendFileProvider is not null)
{
    app.Lifetime.ApplicationStopped.Register(frontendFileProvider.Dispose);
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = frontendFileProvider
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = frontendFileProvider
    });
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

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

public partial class Program;
