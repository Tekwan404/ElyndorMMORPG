using Elyndor.Core.Content;
using Elyndor.Infrastructure.Content;

namespace Elyndor.IntegrationTests.Content;

public sealed class GameContentPackageLoaderTests
{
    [Fact]
    public async Task LoadAsyncReturnsValidatedPackage()
    {
        const string json = """
            {
              "contentVersion": "0.1.0",
              "balanceVersion": "0.1.0",
              "publishedAtUtc": "2026-08-29T00:00:00+00:00",
              "definitions": [],
              "locations": [
                {
                  "id": "STARTER_TOWN",
                  "displayName": "Starter Town",
                  "dangerLevel": "SAFE",
                  "recommendedLevel": 1,
                  "transitions": []
                }
              ]
            }
            """;

        await WithTemporaryPackageAsync(json, async path =>
        {
            GameContentPackage package = await GameContentPackageLoader.LoadAsync(path);

            Assert.Equal("0.1.0", package.ContentVersion);
            Assert.Empty(package.Definitions);
            Assert.Equal("STARTER_TOWN", Assert.Single(package.Locations).Id);
        });
    }

    [Fact]
    public async Task LoadAsyncRejectsUnknownJsonProperties()
    {
        const string json = """
            {
              "contentVersion": "0.1.0",
              "balanceVersion": "0.1.0",
              "publishedAtUtc": "2026-08-29T00:00:00+00:00",
              "definitions": [],
              "locations": [],
              "unexpected": true
            }
            """;

        await WithTemporaryPackageAsync(json, async path =>
        {
            await Assert.ThrowsAsync<InvalidDataException>(
                () => GameContentPackageLoader.LoadAsync(path));
        });
    }

    [Fact]
    public async Task LoadAsyncRejectsSemanticValidationErrors()
    {
        const string json = """
            {
              "contentVersion": "0.1.0",
              "balanceVersion": "0.1.0",
              "publishedAtUtc": "2026-08-29T00:00:00+00:00",
              "definitions": [
                {
                  "type": "CLASS",
                  "id": "WARRIOR",
                  "references": [
                    { "type": "ABILITY", "id": "MISSING" }
                  ]
                }
              ],
              "locations": []
            }
            """;

        await WithTemporaryPackageAsync(json, async path =>
        {
            ContentPackageValidationException exception =
                await Assert.ThrowsAsync<ContentPackageValidationException>(
                    () => GameContentPackageLoader.LoadAsync(path));

            Assert.Contains(exception.Errors, error => error.Code == "MISSING_REFERENCE");
        });
    }

    private static async Task WithTemporaryPackageAsync(
        string json,
        Func<string, Task> assertion)
    {
        string path = Path.GetTempFileName();

        try
        {
            await File.WriteAllTextAsync(path, json);
            await assertion(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
