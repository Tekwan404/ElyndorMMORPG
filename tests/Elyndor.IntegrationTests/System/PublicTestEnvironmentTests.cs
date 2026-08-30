using Elyndor.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Elyndor.IntegrationTests.System;

public sealed class PublicTestEnvironmentTests
{
    [Fact]
    public async Task PublicTestExposesHealthWithoutDevelopmentOpenApi()
    {
        await using WebApplicationFactory<Program> factory =
            new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("PublicTest");
                    builder.UseTestAuthentication();
                    builder.UseSetting(
                        "ConnectionStrings:game",
                        "Host=localhost;Port=5432;Database=elyndor_tests;Username=postgres;Password=postgres");
                });

        using HttpClient client = factory.CreateClient();

        HttpResponseMessage healthResponse = await client.GetAsync("/alive");
        HttpResponseMessage openApiResponse = await client.GetAsync("/openapi/v1.json");
        HttpResponseMessage developmentAuthResponse = await client.PostAsync(
            "/api/v1/auth/development",
            content: null);

        healthResponse.EnsureSuccessStatusCode();
        Assert.Equal("Healthy", await healthResponse.Content.ReadAsStringAsync());
        Assert.Equal(global::System.Net.HttpStatusCode.NotFound, openApiResponse.StatusCode);
        Assert.Equal(
            global::System.Net.HttpStatusCode.NotFound,
            developmentAuthResponse.StatusCode);
    }

    [Fact]
    public async Task UnknownApiRouteDoesNotReturnFrontendIndex()
    {
        await using WebApplicationFactory<Program> factory =
            new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseTestAuthentication();
                    builder.UseSetting(
                        "ConnectionStrings:game",
                        "Host=localhost;Port=5432;Database=elyndor_tests;Username=postgres;Password=postgres");
                });

        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/missing");

        Assert.Equal(global::System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}
