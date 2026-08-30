using Elyndor.Core.Content;
using Elyndor.Infrastructure.Content;

string packagePath = Path.GetFullPath(
    args.Length > 0
        ? args[0]
        : Path.Combine(Environment.CurrentDirectory, "content", "package.json"));

try
{
    GameContentPackage package = await GameContentPackageLoader.LoadAsync(packagePath);
    Console.WriteLine(
        $"Content package valid: ContentVersion={package.ContentVersion}, "
        + $"BalanceVersion={package.BalanceVersion}, Definitions={package.Definitions.Count}, "
        + $"Locations={package.Locations.Count}");

    return 0;
}
catch (ContentPackageValidationException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
catch (InvalidDataException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
catch (IOException exception)
{
    Console.Error.WriteLine($"Unable to read content package '{packagePath}': {exception.Message}");
    return 1;
}
catch (UnauthorizedAccessException exception)
{
    Console.Error.WriteLine($"Unable to read content package '{packagePath}': {exception.Message}");
    return 1;
}
