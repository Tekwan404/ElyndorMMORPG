using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Content;
using Elyndor.Core.Items;

namespace Elyndor.ContentValidator;

public sealed record AffixEnvelopeAudit(int ItemLevel, decimal PublishedBudget, decimal EffectiveBudget,
    string StatId, bool Guaranteed, decimal Min, decimal Max, decimal Step);
public sealed record ItemAffixAuditEntry(ItemDefinition Definition, IReadOnlyList<string> EligibleRandomStatIds,
    IReadOnlyList<AffixEnvelopeAudit> Envelopes, IReadOnlyList<string> Issues);

public static class ItemAffixAudit
{
    public static IReadOnlyList<ItemAffixAuditEntry> Create(GameContentPackage package) =>
        (package.Items ?? []).OrderBy(item => item.RequiredLevel).ThenBy(item => item.Id, StringComparer.Ordinal)
            .Select(item => Inspect(item, package.Itemization)).ToArray();

    private static ItemAffixAuditEntry Inspect(ItemDefinition item, ItemizationDefinition? itemization)
    {
        if (!ProceduralItemPolicy.IsEnabled(item) || itemization is null)
            return new(item, [], [], []);
        ItemAffixPoolDefinition pool = itemization.AffixPools.Single(candidate => candidate.Id == item.RandomAffixPoolId);
        string[] guaranteed = (item.GuaranteedAffixStatIds ?? []).ToArray();
        IReadOnlyList<string> eligible = ItemAffixEligibilityPolicy.GetCandidates(item, itemization, pool, guaranteed);
        List<string> issues = [];
        ItemAffixCountProfileDefinition counts = itemization.AffixCountProfiles.Single(profile => profile.Id == item.AffixCountProfileId);
        if (!ItemAffixEligibilityPolicy.CanAlwaysFill(pool, eligible, guaranteed, counts.MaximumBonusCount))
            issues.Add("UNFILLABLE_COMBINATION");
        List<AffixEnvelopeAudit> envelopes = [];
        ItemizationDefinition normalized = ItemizationBudgetPolicy.NormalizeForTemplate(item, itemization);
        // Force the inspected stat to be the first random pick, using the real generator
        // and its unchanged maximum affix count/budget rather than duplicating range formulas.
        for (int level = item.ItemLevelMin ?? item.RequiredLevel; level <= (item.ItemLevelMax ?? item.ItemLevelMin ?? item.RequiredLevel); level++)
        {
            foreach (string statId in guaranteed.Concat(counts.MaximumBonusCount > 0 ? eligible : []).Distinct(StringComparer.Ordinal))
            {
                ItemAffixPoolDefinition orderedPool = pool with { StatIds = [statId, .. pool.StatIds.Where(id => id != statId)] };
                ItemizationDefinition probe = normalized with
                {
                    AffixPools = normalized.AffixPools.Select(candidate => candidate.Id == pool.Id ? orderedPool : candidate).ToArray(),
                    AffixCountProfiles = normalized.AffixCountProfiles.Select(profile => profile.Id == counts.Id
                        ? profile with { MinimumBonusCount = profile.MaximumBonusCount } : profile).ToArray()
                };
                GeneratedItemInstance generated = ItemInstanceGenerator.Generate(item, probe, probe.QualityProfiles[0].Id,
                    new ZeroRandom(), overrides: new ItemGenerationOverrides(level, level));
                GeneratedItemAffix affix = generated.Affixes.Single(candidate => candidate.StatId == statId);
                envelopes.Add(new(level, ItemInstanceGenerator.CalculateTemplateMaxPower(item, itemization, level),
                    generated.MaxTemplateItemPower, statId, affix.IsGuaranteed, affix.MinAtGeneration, affix.MaxAtGeneration, affix.StepAtGeneration));
                if (affix.MinAtGeneration >= affix.MaxAtGeneration) issues.Add($"COLLAPSED_RANGE:{level}:{statId}");
            }
        }
        return new(item, eligible, envelopes, issues);
    }

    private sealed class ZeroRandom : IGameRandom
    {
        public decimal NextUnit() => 0;
    }
}
