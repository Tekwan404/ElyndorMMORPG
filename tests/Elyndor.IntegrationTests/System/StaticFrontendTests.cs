using Elyndor.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Elyndor.IntegrationTests.System;

public sealed class StaticFrontendTests
{
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
                new WebApplicationFactory<Program>()
                    .WithWebHostBuilder(builder =>
                    {
                        builder.UseTestAuthentication();
                        builder.UseSetting("Frontend:DistPath", frontendDirectory);
                        builder.UseSetting(
                            "ConnectionStrings:game",
                            "Host=localhost;Port=5432;Database=elyndor_tests;Username=postgres;Password=postgres");
                    });

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
}
