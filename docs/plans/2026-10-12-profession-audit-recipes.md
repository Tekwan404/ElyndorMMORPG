# Profession audit and useful recipes

Scope: audit gathering, crafting, knowledge and consumable use; preserve server authority, transaction replay protection and existing effects.

1. Reproduce skinning client RNG, spatial-artifact capacity and mining during AFK with integration regressions.
2. Fix those defects using existing RNG and projected inventory services; restore contextual alchemy filter.
3. Add six resource potions, eight blacksmith recipes and five enchantment recipes at skills 1/16/31. Keep existing IDs and scroll knowledge gates.
4. Verify actual crafting and potion consumption with replay, resource restoration and remaining ingredients. Validate content and all backend/frontend suites; update draft PR 367.

No migration or new dependencies needed. Resource potions retain the 30-second RESOURCE_POTION cooldown. Craft equipment follows existing itemization templates. No new combat effect types.
