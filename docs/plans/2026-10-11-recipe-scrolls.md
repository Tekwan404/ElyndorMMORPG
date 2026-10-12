# Recipe scrolls: loot → study → craft

User chose recipe scrolls from loot. Extend the existing herbalism/alchemy PR with additional potion and leather armor recipes, keeping automatic baseline recipes compatible.

- Add a Recipe item category, authored scroll items and optional LearningScrollItemId on recipe definitions. All drop chances, skills, ingredients and output stats remain content data.
- Persist learned recipe IDs under the learned profession; cascade deletion on forgetting provides the user-approved complete reset.
- Studying consumes one unlocked scroll in a serializable transaction with character locking and a durable mutation fingerprint. Validate profession, skill, station, travel/combat and replay before consuming. Replaying study after forgetting never restores knowledge.
- Craft requires learned knowledge for scroll recipes and otherwise preserves existing transactions and output generation.
- Extend existing workshop rows with study requirements/actions and canonical scroll names. Recipe items appear under inventory supplies with instructions to study in the city workshop.
- Cover study/replay, locked scrolls, concurrent requests, skill/location rejection, study → craft and forget → replay with PostgreSQL tests; validate loot reachability, Vue states, mobile UI and the full test/build pipeline.

Scope: four additional potion recipes using existing effects, and nine armor recipes completing head/shoulders/legs across the three existing leather tiers. No heavy-armor profession or new effect system.
