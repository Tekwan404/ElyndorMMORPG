using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Elyndor.Contracts.Economy;

/// <summary>Money is integer bronze. Large balances use decimal strings to avoid JavaScript rounding.</summary>
public sealed class MoneyJsonConverter : JsonConverter<long>
{
    private const long MaxSafeJavaScriptInteger = 9_007_199_254_740_991;

    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        long value;
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out value) && value >= 0)
            return value;
        if (reader.TokenType == JsonTokenType.String
            && long.TryParse(reader.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= 0)
            return value;
        throw new JsonException("Money must be non-negative integer bronze units.");
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        if (value < 0) throw new JsonException("Money cannot be negative.");
        if (value > MaxSafeJavaScriptInteger)
            writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
        else
            writer.WriteNumberValue(value);
    }
}
