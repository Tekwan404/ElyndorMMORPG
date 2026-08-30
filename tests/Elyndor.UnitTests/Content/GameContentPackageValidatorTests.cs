using Elyndor.Core.Content;

namespace Elyndor.UnitTests.Content;

public sealed class GameContentPackageValidatorTests
{
    private static readonly DateTimeOffset PublishedAtUtc =
        new(2026, 8, 29, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ValidateAcceptsTypedReferencesToExistingDefinitions()
    {
        GameContentPackage package = CreatePackage(
            new GameContentDefinition(
                "CLASS",
                "WARRIOR",
                [new GameContentReference("ABILITY", "BASIC_ATTACK")]),
            new GameContentDefinition("ABILITY", "BASIC_ATTACK", []));

        IReadOnlyList<ContentValidationError> errors =
            GameContentPackageValidator.Validate(package);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateRejectsDuplicateIdsWithinTheSameDefinitionType()
    {
        GameContentPackage package = CreatePackage(
            new GameContentDefinition("CLASS", "WARRIOR", []),
            new GameContentDefinition("CLASS", "WARRIOR", []));

        IReadOnlyList<ContentValidationError> errors =
            GameContentPackageValidator.Validate(package);

        Assert.Contains(errors, error => error.Code == "DUPLICATE_DEFINITION_ID");
    }

    [Fact]
    public void ValidateRejectsMissingTypedReference()
    {
        GameContentPackage package = CreatePackage(
            new GameContentDefinition(
                "CLASS",
                "WARRIOR",
                [new GameContentReference("ABILITY", "MISSING_ABILITY")]));

        IReadOnlyList<ContentValidationError> errors =
            GameContentPackageValidator.Validate(package);

        Assert.Contains(errors, error => error.Code == "MISSING_REFERENCE");
    }

    [Theory]
    [InlineData("class", "WARRIOR", "INVALID_DEFINITION_TYPE")]
    [InlineData("CLASS", "warrior", "INVALID_DEFINITION_ID")]
    public void ValidateRejectsNonCanonicalIdentifiers(
        string definitionType,
        string definitionId,
        string expectedErrorCode)
    {
        GameContentPackage package = CreatePackage(
            new GameContentDefinition(definitionType, definitionId, []));

        IReadOnlyList<ContentValidationError> errors =
            GameContentPackageValidator.Validate(package);

        Assert.Contains(errors, error => error.Code == expectedErrorCode);
    }

    [Fact]
    public void ValidateRejectsMissingVersionsAndNonUtcPublicationTime()
    {
        GameContentPackage package = new(
            string.Empty,
            " ",
            PublishedAtUtc.ToOffset(TimeSpan.FromHours(5)),
            []);

        IReadOnlyList<ContentValidationError> errors =
            GameContentPackageValidator.Validate(package);

        Assert.Contains(errors, error => error.Code == "MISSING_CONTENT_VERSION");
        Assert.Contains(errors, error => error.Code == "MISSING_BALANCE_VERSION");
        Assert.Contains(errors, error => error.Code == "PUBLISHED_AT_NOT_UTC");
    }

    private static GameContentPackage CreatePackage(params GameContentDefinition[] definitions) =>
        new("0.1.0", "0.1.0", PublishedAtUtc, definitions);
}
