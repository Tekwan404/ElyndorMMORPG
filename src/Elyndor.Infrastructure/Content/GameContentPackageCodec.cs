using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Elyndor.Core.Content;

namespace Elyndor.Infrastructure.Content;

public static class GameContentPackageCodec
{
    public static string SerializeCanonical(GameContentPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        return JsonSerializer.Serialize(package, GameContentJson.SerializerOptions);
    }

    public static GameContentPackage DeserializeValidated(string payloadJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);

        GameContentPackage package;
        try
        {
            package = JsonSerializer.Deserialize<GameContentPackage>(
                payloadJson,
                GameContentJson.SerializerOptions)
                ?? throw new InvalidDataException("Content revision payload is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "Content revision payload does not match the required JSON shape.",
                exception);
        }

        IReadOnlyList<ContentValidationError> errors =
            ContentValidationPipeline.Default.Validate(package);
        if (errors.Count > 0)
            throw new ContentPackageValidationException(errors);

        _ = GameContentIndexes.For(package);
        return package;
    }

    public static string ComputeSha256(string payloadJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)));
    }
}

public sealed record ContentPackageParityResult(
    bool IsMatch,
    string SourceSha256,
    string RoundTripSha256);

public static class ContentPackageParityVerifier
{
    public static ContentPackageParityResult Verify(
        GameContentPackage source,
        string canonicalPayloadJson)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalPayloadJson);

        string sourceCanonical = GameContentPackageCodec.SerializeCanonical(source);
        GameContentPackage restored =
            GameContentPackageCodec.DeserializeValidated(canonicalPayloadJson);
        string restoredCanonical =
            GameContentPackageCodec.SerializeCanonical(restored);

        string sourceSha256 =
            GameContentPackageCodec.ComputeSha256(sourceCanonical);
        string roundTripSha256 =
            GameContentPackageCodec.ComputeSha256(restoredCanonical);

        return new ContentPackageParityResult(
            string.Equals(sourceCanonical, restoredCanonical, StringComparison.Ordinal),
            sourceSha256,
            roundTripSha256);
    }
}

public sealed class ContentPackageParityException(
    string sourceSha256,
    string roundTripSha256)
    : InvalidDataException(
        $"Content package parity failed. Source SHA-256: {sourceSha256}; round-trip SHA-256: {roundTripSha256}.")
{
    public string SourceSha256 { get; } = sourceSha256;
    public string RoundTripSha256 { get; } = roundTripSha256;
}
