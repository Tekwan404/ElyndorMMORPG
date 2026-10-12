# Profession workshops

The production slice contains Skinning, Leatherworking, Herbalism, Alchemy, Mining, Blacksmithing and Enchanting. Skill capacity remains 300; current active gathering and processing steps are:

| Location | Practice range | Entry skill | Next-step skill | Hide |
| --- | --- | --- | --- | --- |
| Whispering Forest | 1–15 | 1 | 16 | Rough |
| Flower Meadow | 16–30 | 16 | 31 | Light |
| Deep Forest | 31–60 | 31 | 61 | Thick |

All current skinnable creatures within a location share its entry and skill-up ceiling. Spiders still give chitin. Reaching 15 alone does not unlock the next zone: a final successful skill increase to 16 completes the step. Skill-up chance follows the existing declining probability curve with injectable server randomness. At 16/31/61 the previous step no longer gives skill, but still yields materials. At 61 the UI explicitly says that the current available ladder is complete; no nonexistent next zone is promised. This is profession skill, not character level or a change to combat encounters.

Legacy/off-roster skinning entries and IDs remain compatible. Material guides derive from effective location encounters, not from unused monster definitions. The moved encounter roster had no source learnable at skill 1: the existing Whispering Forest wolf now starts the rough-hide step.

Leatherworking uses the same three entry thresholds. Fourteen recipes include three processing recipes, two legacy-hide conversions, and three equipment recipes per material tier. Rough leather produces the existing Common Whispering Forest gear. Light leather produces existing Uncommon Grey Trail gear at character level 6. Thick leather produces three Uncommon level-12 items using existing artwork and the ordinary level-12 affix budget (Agility/Stamina guarantees). Their base armor is conservatively retained from the Grey Trail templates; no new armor coefficient or Rare+ mass production is introduced. Character item instances continue through ItemInstancePersistenceFactory.

## API and UI

`GET /api/v1/professions/` includes canonical material names and actual material-source locations, recipe skill-up limits/result metadata, and corpse yield names/ranges. The added fields are optional in frontend contracts for compatibility with older fixtures. No persistence migration or player inventory rewrite is needed.

The city craft district separates enhancement/reforging in the existing forge from production in the leather workshop. The workshop offers processing/equipment filters, ingredient amounts, explicit skill/station/material requirements and gathering routes. Locked, equipped and transaction-locked ingredients are excluded from displayed availability, matching the server. Skinning aftermath shows the expected raw material and quantity range.

## Economic boundaries and verification

Crafting still uses the existing serializable transaction and mutation fingerprint. Input consumption, output generation and skill increase happen together. Replays do not consume ingredients or create another item. Full-inventory and stacking safeguards stay in the existing service.

Regression coverage checks live-source reachability from skill 1, uniform location steps, ordinary equipment outputs for processed tiers, canonical Russian names, protected ingredient availability, disabled reasons, recipe filters and the highest-step message. PostgreSQL coverage verifies a thick-leather craft and replay; mobile Playwright covers city entry, processing, resource refresh and restored profession state.

Further gathering tiers and additional professions are separate future slices. Raising current gathering/recipe ceilings to 300 without new meaningful sources and products is not part of this release.

## Mining, blacksmithing and enchanting (2026-10-11)

Mining offers copper in Whispering Forest (skill 1), tin in Flower Meadow (16) and iron in Deep Forest (31), following the existing 16/31/61 practice ceilings. Each gathering action yields two ore with a shared 60-second server cooldown. Cooldown is derived from committed mutations as well as profession state, so forgetting/relearning cannot bypass it. The serializable transaction checks the actual location, travel state, skill and inventory capacity and active AFK before granting materials. Smelting produces copper, bronze and iron bars. Twelve blacksmith recipes use ordinary Common/Uncommon level-1/2/6/10 equipment budgets and existing item artwork; there are no set bonuses or mass-produced Rare+ items.

Enchanting destroys an unequipped, unlocked Common item for one Strange Dust, or an Uncommon item for one Magic Essence. Other rarities are deliberately unavailable. Eight recipes at skills 1/16/31 produce scrolls for +1 Stamina/Strength/Agility/Intellect and +2 versions of those stats at skill 31. Applying a scroll requires Enchanting, the city workshop and unequipped chest/gloves/boots; it consumes one scroll and replaces the existing enchantment. Persisted enchantment values are separate from rolled stats, affixes, stars and enhancement. InventorySnapshotReader applies the bonus after structural enhancement and affixes, so the ordinary equipment/combat projection uses it without changing the item's birth roll.

All new mutations use character-scoped intent fingerprints and serializable transactions. Replays return success without granting or consuming anything again. Forgetting a profession requires UI confirmation and resets only its learned skill; items and existing enchantments remain. The two-primary-profession limit is unchanged.

The unified mobile workshop displays all seven professions with generated illustrations, trained skills, selected-profession recipes, material guides, mining routes and explicit destructive-action confirmations. Current content ends at skill 61 while overall capacity remains 300. No timed craft queue, global shared veins, Rare+ disenchanting or high-tier enchants are implied.

The EF migration adds nullable mining cooldown and enchantment identity/stat columns plus a zero-default enchantment value. Existing items and professions retain their current behavior. Regression coverage includes parallel gathering, retry replay, forget/relearn cooldown, protected-item destruction, replacement without stat stacking and effective inventory stats.

## City craft district

The city opens one full-screen illustrated district map with seven readable, tappable pins: five profession stations, equipment enhancement/reforging and profession learning. Leatherworking, Alchemy, Mining (smelting), Blacksmithing and Enchanting each open the matching profession, putting its learning row first if it is not learned. Unlearned stations never show another profession's recipes. Back returns to the district; the district returns to the city. Existing equipment enhancement/reforging remains a separate working service. Mining routes still require the actual outdoor location.

Mining and recipe skill-up rolls use injected server randomness, independent of mutation IDs. Workshop actions reject active travel before consuming materials.

Crafted equipment uses a separate server-generated operation ID for item generation, stable across execution-strategy retries. Client mutation IDs only identify replay requests and cannot select affixes.
## Herbalism and alchemy

Herbalism and Alchemy extend the same profession service and city workshop. Their active skill steps are 1–15, 16–30 and 31–60, with capacity 300. Whispering Forest has forest mint and silverleaf, Flower Meadow has dewy blossom and honey grass, and Deep Forest has shadow moss and hushroot. Each herb has three shared collection points. Existing profession progress is preserved; the two-profession limit remains unchanged.

The location scene exposes resource markers from composed `resourceSources` content. `POST /api/v1/professions/gather` accepts only a node ID and mutation ID. PostgreSQL stores global availability and durable harvest receipts. Serializable transactions lock the character and node, validate location, travel, AFK, skill and capacity, then atomically grant material, increase skill and set a 30-second respawn. The endpoint uses the existing combat operation guard. Competing players cannot harvest one spawn twice; replay after restart or respawn returns the original result. Inventory-full failures leave availability and skill unchanged.

Alchemy offers eighteen baseline recipes across the three steps, using ordinary healing, mana, focus and rage potions with their existing effects and cooldowns. Later recipes yield multiple small utility potions; stronger healing potions retain existing character-level requirements. No new consumable or combat engine is introduced. Herbs and alchemy skill progression use the injectable server RNG; mutation IDs cannot select their rolls. Skinning yield and skill now also use server RNG; corpse receipts preserve the committed result on replay.

Migration `AddResourceGathering` adds only node availability and receipt tables. Existing characters, inventory and professions are not rewritten. The durable receipt table grows with successful collections; a future retention policy must preserve replay protection before deleting receipts. Higher skill content beyond 61 and rare herbs remain separate work.

The player may explicitly forget a profession in the city workshop after a warning and confirmation. `POST /api/v1/professions/forget` deletes its learned skill in an idempotent transaction; learning again starts at 1. Inventory and previous mutation/harvest receipts remain intact. Replaying an old forget request cannot delete a subsequently relearned profession. Forgetting uses the same account advisory lock as learning, plus the character row lock and combat guard.

## Recipe scrolls

Baseline recipes remain available automatically at their required skill. Thirteen additional recipes require a corresponding scroll from ordinary encounter loot: nine leather head/shoulder/leg recipes across the three tiers, and four potion recipes (minor healing, battle tonic, antidote, superior healing). Alchemy uses the existing consumable effects and character-level restrictions. Dense leather gains three level-12 templates matching the existing crafting tier and artwork.

Scrolls are stackable Recipe items. The first three locations append them to their current encounter loot tables without replacing ordinary drops. Each tier has an expected 0.10 scrolls per kill: five independent 2% entries in Whispering Forest, four independent 2.5% entries in Flower Meadow and Deep Forest. These are independent rolls, so a kill can yield zero or several scrolls. Scrolls are not restricted by the player's professions.

`POST /api/v1/professions/study` accepts recipeId and mutationId. The existing city workshop validates profession, skill and an unlocked, unequipped scroll. Account and character locks plus a serializable transaction consume one scroll and persist knowledge with a durable request fingerprint. Replaying a committed study neither consumes another scroll nor restores knowledge after forgetting. Craft validates knowledge for scroll recipes and preserves baseline behavior. Migration RecipeScrollKnowledge adds only the knowledge table; deleting a learned profession cascades its recipe knowledge. Existing skill and inventory are unchanged.

Crafted equipment now uses a server-generated operation ID for item generation; the client mutation ID only identifies a retry. All production profession skill rolls use injectable server RNG with the existing probability curve. Committed items and mutation records preserve replay protection; rerolling cannot be requested through a repeated mutation ID.

The character HUD no longer duplicates the current location above experience. Location and travel remain visible through the world/location screens.

## Profession audit and recipe expansion (2026-10-12)

Content/balance versions are 0.55.0 / 0.45.0. There are 68 recipes: Leatherworking 23, Alchemy 22, Blacksmithing 12, Enchanting 8 and Mining 3. Skinning and Herbalism produce materials through gathering rather than artificial crafting recipes. The existing thirteen loot-scroll recipes retain their knowledge gates.

Six new resource potions use the existing RestoreResource action and the shared 30-second RESOURCE_POTION cooldown:

| Skill / character level | Mana | Focus | Rage | Ingredients per potion |
| --- | --- | --- | --- | --- |
| 16 / 5 | 120 | 50 | 45 | Dewy Blossom 3 + Honey Grass 3 |
| 31 / 8 | 160 | 70 | 60 | Shadow Moss 4 + Hushroot 4 |

They restore more resource in a single use, cost higher-tier herbs and retain resource-type matching and caps. Existing batch recipes and healing potions are preserved. Battle tonic and antidote remain scroll-learned and combat-only. No new combat effect or persistence migration is introduced by this expansion.

The audit fixed client-selectable Skinning randomness, a full-backpack crafting rejection caused by counting the equipped spatial artifact, Mining during active AFK, and the Alchemy filter label. Crafting now delegates projected capacity to InventoryCapacity, including ingredient slots released before output creation. Regression tests reproduce all three server defects. Real PostgreSQL tests exercise all nineteen new recipes, potion use/replay, enchantment application and concurrent crafting from one ingredient stack. Full Release verification passed 1497 unit and 710 integration tests. Frontend verification passed 562 tests, build/typecheck and three mobile workshop scenarios; browser layout scenarios use the mock API while mutation persistence is covered by PostgreSQL tests.
