using Elyndor.ContentValidator;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
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

    string itemAssetRoot = FindItemAssetRoot(packagePath);
    ItemIconAuditReport itemIconAudit = ItemIconAuditReport.Create(
        package.Items ?? Array.Empty<ItemDefinition>(),
        itemAssetRoot);
    Console.WriteLine(
        $"Item icon audit: Total={itemIconAudit.TotalItems}, "
        + $"WithIconId={itemIconAudit.WithIconId}, WithoutIconId={itemIconAudit.WithoutIconId}, "
        + $"Valid={itemIconAudit.Valid}, Missing={itemIconAudit.Missing}, "
        + $"CaseMismatch={itemIconAudit.CaseMismatch}, LegacyOnlyMappings={itemIconAudit.LegacyOnlyMappings}, "
        + $"InvalidPath={itemIconAudit.InvalidPath}, UnsupportedFormat={itemIconAudit.UnsupportedFormat}, "
        + $"Ambiguous={itemIconAudit.Ambiguous}");

    if (!itemIconAudit.IsValid)
    {
        foreach (ItemIconAuditEntry entry in itemIconAudit.Entries.Where(entry => entry.IsBroken))
        {
            Console.Error.WriteLine(
                $"ITEM_ICON_{entry.Status.ToString().ToUpperInvariant()}: "
                + $"item={entry.ItemId} iconId={entry.IconId ?? "<null>"} "
                + $"canonical={entry.CanonicalIconId ?? "<none>"} {entry.Message}");
        }

        throw new InvalidDataException(
            $"Item icon validation failed with {itemIconAudit.Broken} broken reference(s).");
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

static string FindItemAssetRoot(string packagePath)
{
    DirectoryInfo? directory = new FileInfo(packagePath).Directory;
    while (directory is not null)
    {
        string candidate = Path.Combine(
            directory.FullName,
            "web",
            "elyndor-web",
            "src",
            "assets",
            "items");
        if (Directory.Exists(candidate))
            return candidate;
        directory = directory.Parent;
    }

    throw new DirectoryNotFoundException(
        $"Unable to locate web/elyndor-web/src/assets/items above content package '{packagePath}'.");
}
