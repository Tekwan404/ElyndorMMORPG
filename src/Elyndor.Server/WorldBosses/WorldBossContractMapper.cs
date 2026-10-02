using Elyndor.Contracts.Items;
using Elyndor.Contracts.WorldBosses;
using Elyndor.Core.Items;
using Elyndor.Core.WorldBosses;
using Elyndor.Infrastructure.WorldBosses;

namespace Elyndor.Server.WorldBosses;

internal static class WorldBossContractMapper
{
    public static WorldBossRewardResponse ToRewardResponse(
        WorldBossRewardSnapshot reward) =>
        new(
            reward.SpawnId,
            reward.Contribution,
            reward.Tier.ToString(),
            reward.Experience,
            reward.BossGold,
            reward.ChestGold,
            reward.TotalGold,
            reward.Items.Select(ToItemResponse).ToArray(),
            reward.SettledAtUtc);

    public static WorldBossRewardResponse ToRewardResponse(
        Guid spawnId,
        WorldBossSettlementCharacterResult reward) =>
        new(
            spawnId,
            reward.Contribution,
            reward.Tier.ToString(),
            reward.Experience,
            reward.BossGold,
            reward.ChestGold,
            checked(reward.BossGold + reward.ChestGold),
            reward.Items.Select(ToItemResponse).ToArray(),
            reward.SettledAtUtc);

    private static WorldBossRewardItemResponse ToItemResponse(
        WorldBossLootItemResult item) =>
        new(
            item.ItemId,
            item.Name,
            item.Rarity.ToString(),
            item.Quantity,
            item.IconId,
            item.InstanceId,
            item.Pending,
            ToStatsResponse(item.Stats),
            ToGeneratedItemResponse(item.GeneratedItem),
            item.Stats?.WeaponDamageMin,
            item.Stats?.WeaponDamageMax,
            item.Stats?.BlockChance ?? 0m,
            item.Stats?.BlockValueMin ?? 0m,
            item.Stats?.BlockValueMax ?? 0m);

    private static ItemStatsResponse? ToStatsResponse(WorldBossLootItemStats? stats) =>
        stats is null
            ? null
            : new ItemStatsResponse(
                stats.Strength,
                stats.Agility,
                stats.Intellect,
                stats.Stamina,
                stats.MaxHp,
                stats.AttackPower,
                stats.SpellPower,
                stats.CriticalChance,
                stats.CriticalDamage,
                stats.Accuracy,
                stats.Armor,
                stats.MagicResistance,
                stats.Dodge,
                stats.ArmorPenetration,
                stats.MagicPenetration,
                stats.AttackSpeed,
                stats.MaxResource);

    private static GeneratedItemSummaryResponse? ToGeneratedItemResponse(
        GeneratedItemInstance? generated) =>
        generated is null
            ? null
            : new GeneratedItemSummaryResponse(
                generated.ItemLevel,
                generated.ActualItemPower,
                generated.MaxTemplateItemPower,
                generated.RollQuality,
                generated.Stars,
                generated.IsPerfect,
                generated.PerfectOrigin,
                generated.GeneratedPrefixId,
                generated.GeneratedSuffixId,
                generated.DisplayName,
                generated.Affixes
                    .OrderBy(affix => affix.GenerationOrdinal)
                    .Select(affix => new ItemAffixResponse(
                        affix.SlotKey,
                        affix.StatId,
                        affix.Value,
                        affix.MinAtGeneration,
                        affix.MaxAtGeneration,
                        affix.StepAtGeneration,
                        affix.AffixTier,
                        affix.IsGuaranteed,
                        affix.IsReforgeSlot))
                    .ToArray());
}
