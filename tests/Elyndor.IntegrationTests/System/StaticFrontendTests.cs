using Elyndor.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Elyndor.IntegrationTests.System;

public sealed class StaticFrontendTests
{
    [Fact]
    public async Task GetHashedFrontendAssetReturnsLongLivedImmutableCacheHeader()
    {
        string frontendDirectory = Path.Combine(
            Path.GetTempPath(),
            $"elyndor-frontend-{Guid.NewGuid():N}");
        string assetsDirectory = Path.Combine(frontendDirectory, "assets");
        Directory.CreateDirectory(assetsDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(frontendDirectory, "index.html"),
            "<!doctype html><title>Elyndor cache test</title>");
        await File.WriteAllTextAsync(
            Path.Combine(assetsDirectory, "app-ABC123.js"),
            "console.log('cached');");

        try
        {
            await using WebApplicationFactory<Program> factory =
                CreateFactory(frontendDirectory);
            using HttpClient client = factory.CreateClient();

            HttpResponseMessage response = await client.GetAsync("/assets/app-ABC123.js");

            response.EnsureSuccessStatusCode();
            Assert.True(response.Headers.CacheControl?.Public);
            Assert.Equal(TimeSpan.FromDays(365), response.Headers.CacheControl?.MaxAge);
            Assert.Contains(
                response.Headers.CacheControl!.Extensions,
                directive => directive.Name.Equals("immutable", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(frontendDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task GetClientRouteReturnsConfiguredFrontendIndex()
    {
        string frontendDirectory = Path.Combine(
            Path.GetTempPath(),
            $"elyndor-frontend-{Guid.NewGuid():N}");
        Directory.CreateDirectory(frontendDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(frontendDirectory, "index.html"),
            "<!doctype html><title>Elyndor public test</title>");

        try
        {
            await using WebApplicationFactory<Program> factory =
                CreateFactory(frontendDirectory);

            using HttpClient client = factory.CreateClient();

            HttpResponseMessage response = await client.GetAsync("/world");
            string html = await response.Content.ReadAsStringAsync();

            response.EnsureSuccessStatusCode();
            Assert.Contains("Elyndor public test", html, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(frontendDirectory, recursive: true);
        }
    }

    private static WebApplicationFactory<Program> CreateFactory(string frontendDirectory) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseTestAuthentication();
                builder.UseSetting("Frontend:DistPath", frontendDirectory);
                builder.UseSetting(
                    "ConnectionStrings:game",
                    "Host=localhost;Port=5432;Database=elyndor_tests;Username=postgres;Password=postgres");
            });
}
