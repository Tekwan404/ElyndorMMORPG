using Microsoft.AspNetCore.Hosting;

namespace Elyndor.IntegrationTests.Support;

public static class TestAuthenticationSettings
{
    public static IWebHostBuilder UseTestAuthentication(this IWebHostBuilder builder)
    {
        builder.UseSetting("Authentication:Issuer", "Elyndor.Tests");
        builder.UseSetting("Authentication:Audience", "Elyndor.Tests.Client");
        builder.UseSetting(
            "Authentication:SigningKey",
            "non-secret-integration-test-signing-key-with-more-than-32-bytes");
        builder.UseSetting("Authentication:Telegram:BotToken", "123456:TEST_TOKEN");
        builder.UseSetting("Authentication:Development:Enabled", "false");

        return builder;
    }
}
