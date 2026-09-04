using System.Text.Json;
using System.Text.Json.Serialization;

namespace Elyndor.Infrastructure.Content;

internal static class GameContentJson
{
    internal static JsonSerializerOptions SerializerOptions { get; } = CreateOptions();

    internal static async Task<T> ReadRequiredAsync<T>(
        string path,
        CancellationToken cancellationToken)
        where T : class
    {
        await using FileStream stream = File.OpenRead(path);
        T? value = await JsonSerializer.DeserializeAsync<T>(
            stream,
            SerializerOptions,
            cancellationToken);
        return value ?? throw new InvalidDataException($"Content file '{path}' is empty.");
    }

    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
