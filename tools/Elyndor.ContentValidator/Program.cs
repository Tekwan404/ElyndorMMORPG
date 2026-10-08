using Elyndor.ContentValidator;
using Elyndor.Core.Balance;
using Elyndor.Core.Combat.Simulation;
using Elyndor.Core.Content;
using Elyndor.Core.Progression;
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
bool auditBalance = args.Any(argument =>
    string.Equals(argument, "--audit-balance", StringComparison.Ordinal));
bool benchmarkBalance = args.Any(argument =>
    string.Equals(argument, "--benchmark-balance", StringComparison.Ordinal));
bool auditProgression = args.Any(argument =>
    string.Equals(argument, "--audit-progression", StringComparison.Ordinal));
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

    if (args.Contains("--audit-mage-mana", StringComparer.Ordinal))
    {
        CombatSimulationRunner runner = new(package);
        List<MageManaEconomyResult> manaResults = [];
        foreach (int level in new[] { 1, 9, 10, 18, 29, 35, 45, 55, 59, 60 })
        foreach (string branch in new[] { "FIRE", "ARCANE", "FROST" })
        foreach (CombatSimulationGearState gear in new[] { CombatSimulationGearState.None, CombatSimulationGearState.Normal, CombatSimulationGearState.Good })
        {
            MageManaEconomyResult result = runner.RunMageManaEconomy(level, branch, gear);
            manaResults.Add(result);
            Console.WriteLine($"Mana L{level} {branch} {gear}: pool={result.MaxMana:0.##} first-failure={result.SecondsToOom?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none in 600s"} casts={result.Casts}");
        }
        if (analysisExportDirectory is not null)
        {
            Directory.CreateDirectory(analysisExportDirectory);
            await File.WriteAllTextAsync(Path.Combine(analysisExportDirectory, "mage-mana-economy.json"),
                System.Text.Json.JsonSerializer.Serialize(manaResults, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
    }

    if (auditBalance && package.CombatBalance is not null)
    {
        IReadOnlyList<MonsterBalanceAuditEntry> audit = MonsterBalanceAudit.Run(package);
        MonsterBalanceAuditEntry[] outliers = audit
            .Where(item => !item.WithinTolerance)
            .ToArray();
        Console.WriteLine(
            $"Combat balance audit: Monsters={audit.Count}, Outliers={outliers.Length}, "
            + $"Tolerance={package.CombatBalance.AuditTolerancePercent}%");
        foreach (MonsterBalanceAuditEntry item in outliers)
        {
            Console.WriteLine(
                $"Warning: COMBAT_BALANCE_OUTLIER {item.MonsterId} "
                + $"L{item.Level} {item.Rank}/{item.ArchetypeId} "
                + $"HP={item.HpDeltaPercent:+0.##;-0.##;0}% "
                + $"AP={item.AttackPowerDeltaPercent:+0.##;-0.##;0}% "
                + $"Armor={item.ArmorDeltaPercent:+0.##;-0.##;0}% "
                + $"MR={item.MagicResistanceDeltaPercent:+0.##;-0.##;0}% "
                + $"AA={item.AutoAttackBaseDamageDeltaPercent:+0.##;-0.##;0}%");
        }
    }

    if (auditProgression && package.ProgressionBalance is not null)
    {
        IReadOnlyList<ProgressionBalanceAuditRow> rows =
            ProgressionBalanceAudit.Run(package);
        ProgressionBalanceAuditRow[] missingLevels = rows
            .Where(row => !row.HasRewardableNormalMonster)
            .ToArray();
        Console.WriteLine(
            $"Progression balance audit: Levels={rows.Count}, "
            + $"MissingRewardableNormalLevels={missingLevels.Length}");
        foreach (ProgressionBalanceAuditRow row in rows)
        {
            Console.WriteLine(
                $"XP L{row.Level}: next={row.XpToNext} "
                + $"normalMob={row.NormalMonsterXp} "
                + $"targetKills={row.TargetNormalKills:0.#} "
                + $"normalMobs={row.RewardableNormalMonsterCount} "
                + $"quest={row.TargetQuestXp} ({row.TargetQuestSharePercent:0.#}%) "
                + $"combat={row.TargetPureCombatMinutes:0.##}m "
                + $"status={(row.HasRewardableNormalMonster ? "OK" : "NO_NORMAL_MOB")}");
        }
    }

    if (benchmarkBalance)
    {
        CombatBalanceBenchmarkRunner runner = new(package);
        IReadOnlyList<CombatBalanceBenchmarkRow> rows = runner.Run();
        Console.WriteLine(
            $"Combat balance benchmark: Rows={rows.Count}, "
            + $"TTK in target={rows.Count(item => item.TtkWithinTarget)}, "
            + $"TTD in target={rows.Count(item => item.TtdWithinTarget)}");
        foreach (CombatBalanceBenchmarkRow row in rows)
        {
            Console.WriteLine(
                $"{row.ClassId} L{row.Level} {row.GearState} vs {row.MonsterId}: "
                + $"HP={row.PlayerMaxHp:0.#} Armor={row.PlayerArmor:0.#} "
                + $"EHP={row.PlayerPhysicalEhp:0.#} DPS={row.PlayerDps:0.#} "
                + $"TTK={row.EstimatedTtkSeconds:0.##}s TTD={row.EstimatedTtdSeconds:0.##}s "
                + $"Win={row.WinRatePercent:0.#}%");
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
