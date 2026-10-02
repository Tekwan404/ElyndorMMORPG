using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elyndor.Contracts.Economy;
using Elyndor.Contracts.Items;
using Elyndor.Contracts.World;

namespace Elyndor.UnitTests.Economy;

public sealed class MoneySerializationTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new MoneyJsonConverter() } };

    [Theory]
    [InlineData(0L, "0")]
    [InlineData(1254783L, "1254783")]
    [InlineData(9007199254740991L, "9007199254740991")]
    [InlineData(9007199254740992L, "\"9007199254740992\"")]
    [InlineData(long.MaxValue, "\"9223372036854775807\"")]
    public void BronzeBalanceRoundTripsWithoutPrecisionLoss(long amount, string expected)
    {
        string json = JsonSerializer.Serialize(amount, Options);
        Assert.Equal(expected, json);
        Assert.Equal(amount, JsonSerializer.Deserialize<long>(json, Options));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("\"9223372036854775808\"")]
    [InlineData("\"1e4\"")]
    public void InvalidMoneyIsRejected(string json) =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<long>(json, Options));

    [Fact]
    public void WalletResponsesUseExactMoneySerialization()
    {
        Assert.Equal(typeof(MoneyJsonConverter), typeof(BootstrapCharacterResponse).GetProperty("Gold")!
            .GetCustomAttribute<JsonConverterAttribute>()!.ConverterType);
        var response = new MerchantResponse("test", "Test", "", long.MaxValue, [], []);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response, JsonSerializerOptions.Web));
        Assert.Equal("9223372036854775807", json.RootElement.GetProperty("gold").GetString());
    }
}
