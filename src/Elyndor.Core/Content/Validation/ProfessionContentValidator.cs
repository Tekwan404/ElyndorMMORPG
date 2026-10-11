using Elyndor.Core.Items;
using Elyndor.Core.Professions;

namespace Elyndor.Core.Content;

public sealed class ProfessionContentValidator : IContentValidationStage
{
    public void Validate(ContentValidationContext context)
    {
        var package = context.Package;
        var professions = (package.Professions ?? []).Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var items = (package.Items ?? []).GroupBy(item => item.Id).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var locations = package.Locations.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var recipeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var recipe in package.ProfessionRecipes ?? [])
        {
            if (!recipeIds.Add(recipe.Id) || !professions.Contains(recipe.ProfessionId) || !items.ContainsKey(recipe.OutputItemId)
                || recipe.RequiredSkill < 1 || recipe.SkillUpUntil < recipe.RequiredSkill || recipe.OutputQuantity < 1
                || recipe.Ingredients.Count == 0 || recipe.Ingredients.Any(item => item.Quantity < 1 || !items.ContainsKey(item.ItemId))
                || recipe.Ingredients.Select(item => item.ItemId).Distinct().Count() != recipe.Ingredients.Count
                || (recipe.RequiredLocationId is not null && !locations.Contains(recipe.RequiredLocationId)))
                context.Errors.Add(new("PROFESSION_INVALID_RECIPE", recipe.Id, "Recipe references, skill range or quantities are invalid."));
        }
        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in package.ProfessionGatheringNodes ?? [])
        {
            if (!nodeIds.Add(node.Id) || !professions.Contains(ProfessionIds.Mining) || !locations.Contains(node.LocationId)
                || !items.TryGetValue(node.ItemId, out var item) || item.Type != ItemType.Material || !item.Stackable
                || node.Quantity < 1 || node.CooldownSeconds < 1 || node.RequiredSkill < 1 || node.SkillUpUntil <= node.RequiredSkill)
                context.Errors.Add(new("PROFESSION_INVALID_NODE", node.Id, "Gathering node must reference a location and stackable material with valid limits."));
        }
        var enchantmentIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var enchantment in package.ProfessionEnchantments ?? [])
        {
            if (!enchantmentIds.Add(enchantment.Id) || !professions.Contains(ProfessionIds.Enchanting)
                || !items.TryGetValue(enchantment.ScrollItemId, out var scroll) || scroll.Type != ItemType.Material
                || enchantment.Value <= 0 || enchantment.StatId is not ("STRENGTH" or "AGILITY" or "INTELLECT" or "STAMINA")
                || enchantment.Slots.Count == 0 || enchantment.Slots.Any(slot => !EquipmentSlotPolicy.IsCanonical(slot))
                || !(package.ProfessionRecipes ?? []).Any(recipe => recipe.ProfessionId == ProfessionIds.Enchanting && recipe.OutputItemId == enchantment.ScrollItemId))
                context.Errors.Add(new("PROFESSION_INVALID_ENCHANTMENT", enchantment.Id, "Enchantment needs a craftable scroll, supported stat and equipment slots."));
        }
        var rarities = new HashSet<ItemRarity>();
        foreach (var rule in package.ProfessionDisenchanting ?? [])
        {
            if (!rarities.Add(rule.Rarity) || !items.TryGetValue(rule.ItemId, out var item) || item.Type != ItemType.Material
                || !item.Stackable || rule.Quantity < 1)
                context.Errors.Add(new("PROFESSION_INVALID_DISENCHANTING", rule.Rarity.ToString(), "Disenchanting yield must be a unique rarity and valid stackable material."));
        }
    }
}
