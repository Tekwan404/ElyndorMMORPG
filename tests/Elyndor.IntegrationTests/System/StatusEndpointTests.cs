using System.Net.Http.Json;
using Elyndor.Contracts.System;
using Elyndor.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Elyndor.IntegrationTests.System;

public sealed class StatusEndpointTests
{
    [Fact]
    public async Task GetStatusReturnsReadyServerSnapshot()
    {
        await using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseTestAuthentication();
                builder.UseSetting(
                    "ConnectionStrings:game",
                    "Host=localhost;Port=5432;Database=elyndor_tests;Username=postgres;Password=postgres");
            });

        using HttpClient client = factory.CreateClient();

        ApiStatusResponse? response = await client.GetFromJsonAsync<ApiStatusResponse>(
            "/api/v1/status",
            CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("Elyndor.Server", response.Service);
        Assert.Equal("ready", response.Status);
    }
}
