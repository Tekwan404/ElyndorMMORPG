using Elyndor.ContentValidator;
using Elyndor.Core.Content;
using Elyndor.Infrastructure.Content;

bool strictTalents = args.Any(argument =>
    string.Equals(argument, "--strict-talents", StringComparison.Ordinal));
bool auditTalents = args.Any(argument =>
    string.Equals(argument, "--audit-talents", StringComparison.Ordinal));
bool strictItemIcons = args.Any(argument =>
    string.Equals(argument, "--strict-item-icons", StringComparison.Ordinal));
bool auditItemIcons = args.Any(argument =>
    string.Equals(argument, "--audit-item-icons", StringComparison.Ordinal))
    || strictItemIcons;
string? analysisExportDirectory = args
    .FirstOrDefault(argument => argument.StartsWith("--export-analysis=", StringComparison.Ordinal))?
    .Split('=', 2)[1];
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

    if (auditItemIcons)
    {
        string assetRoot = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(packagePath)!,
            "..",
            "web",
            "elyndor-web",
            "src",
            "assets",
            "items"));
        ItemIconAuditReport audit = ItemIconAuditReport.Create(
            package,
            ItemIconAuditReport.ReadAssets(assetRoot));
        Console.WriteLine(
            $"Item icon audit: Total={audit.TotalItemDefinitions}, WithIconId={audit.WithIconId}, "
            + $"WithoutIconId={audit.WithoutIconId}, Valid={audit.Valid}, MissingAsset={audit.MissingAsset}, "
            + $"CaseMismatch={audit.CaseMismatch}, InvalidPath={audit.InvalidPath}, "
            + $"UnsupportedFormat={audit.UnsupportedFormat}, Ambiguous={audit.Ambiguous}, "
            + $"LegacyOnly={audit.LegacyOnly}, UnusedAssets={audit.UnusedAssets}");

        foreach (ItemIconAuditIssue issue in audit.Issues)
            Console.WriteLine($"{issue.Severity}: {issue.Code} {issue.ItemId} ({issue.IconId}): {issue.Message}");

        foreach (string assetId in audit.UnusedAssetIds)
            Console.WriteLine($"Warning: ITEM_ICON_ASSET_UNUSED ASSET:{assetId} ({assetId}): No composed ItemDefinition references this asset.");

        if (strictItemIcons && audit.Issues.Any(issue => issue.Severity == ItemIconIssueSeverity.Error))
            return 1;
    }

    if (auditTalents || strictTalents)
    {
        TalentAuditReport audit = TalentAuditReport.Create(package);
        Console.WriteLine(
            $"Talent audit: Trees={audit.TreeCount}, Branches={audit.BranchCount}, "
            + $"Nodes={audit.NodeCount}, Modifiers={audit.ModifierCount}, "
            + $"DeferredModifiers={audit.DeferredModifierCount}, "
            + $"FullyDeferredNodes={audit.FullyDeferredNodeCount}, "
            + $"MissingRussianText={audit.MissingRussianTextCount}, "
            + $"RuntimeUnmappedModifiers={audit.RuntimeUnmappedModifierCount}");

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

    if (!string.IsNullOrWhiteSpace(analysisExportDirectory))
    {
        string outputDirectory = Path.GetFullPath(analysisExportDirectory);
        await ContentAuditExporter.ExportAsync(package, outputDirectory, CancellationToken.None);
        Console.WriteLine($"Content analysis exported to: {outputDirectory}");
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
