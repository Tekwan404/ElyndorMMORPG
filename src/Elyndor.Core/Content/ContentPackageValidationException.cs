namespace Elyndor.Core.Content;

public sealed class ContentPackageValidationException : Exception
{
    public ContentPackageValidationException(IEnumerable<ContentValidationError> errors)
        : this(errors.ToArray())
    {
    }

    private ContentPackageValidationException(ContentValidationError[] errors)
        : base(CreateMessage(errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<ContentValidationError> Errors { get; }

    private static string CreateMessage(IEnumerable<ContentValidationError> errors) =>
        "Game content validation failed:" + Environment.NewLine
        + string.Join(
            Environment.NewLine,
            errors.Select(error => $"- {error.Code} at {error.Path}: {error.Message}"));
}
