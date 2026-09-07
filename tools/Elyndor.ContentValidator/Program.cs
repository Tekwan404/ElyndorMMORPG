using Elyndor.ContentValidator;
using Elyndor.Core.Content;
using Elyndor.Infrastructure.Content;

bool strictTalents = args.Any(argument =>
    string.Equals(argument, "--strict-talents", StringComparison.Ordinal));
bool auditTalents = args.Any(argument =>
    string.Equals(argument, "--audit-talents", StringComparison.Ordinal));
string? packageArgument = args.FirstOrDefault(argument =>
    !argument.StartsWith("--", StringComparison.Ordinal));
string packagePath = Path.GetFullPath(
    packageArgument ?? Path.Combine(Environment.CurrentDirectory, "content", "package.json"));

try
{
    GameContentPackage package = await GameContentPackageLoader.LoadAsync(packagePath);
    GameContentIndexes indexes = GameContentIndexes.For(package);
    Console.WriteLine(
        $"Content package valid: ContentVersion={package.ContentVersion}, "
        + $"BalanceVersion={package.BalanceVersion}, Definitions={indexes.DefinitionsByKey.Count}, "
        + $"Locations={indexes.LocationsById.Count}, Monsters={indexes.MonstersById.Count}, "
        + $"Items={indexes.ItemsById.Count}");

    if (auditTalents || strictTalents)
    {
        TalentAuditReport audit = TalentAuditReport.Create(package);
        Console.WriteLine(
            $"Talent audit: Trees={audit.TreeCount}, Branches={audit.BranchCount}, "
            + $"Nodes={audit.NodeCount}, Modifiers={audit.ModifierCount}, "
            + $"DeferredModifiers={audit.DeferredModifierCount}, "
            + $"FullyDeferredNodes={audit.FullyDeferredNodeCount}, "
            + $"MissingRussianText={audit.MissingRussianTextCount}");

        if (strictTalents)
        {
            IReadOnlyList<ContentValidationError> errors =
                GameContentPackageValidator.ValidateStrictTalents(package);
            if (errors.Count > 0)
                throw new ContentPackageValidationException(errors);
        }

        if (auditTalents)
        {
            foreach (TalentAuditEntry entry in audit.Entries)
            {
                Console.WriteLine(
                    $"{entry.TreeId}:{entry.BranchId}:{entry.NodeId} "
                    + $"ranks={entry.MaxRank} modifiers={entry.Modifiers.Count}");
            }
        }
    }

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
