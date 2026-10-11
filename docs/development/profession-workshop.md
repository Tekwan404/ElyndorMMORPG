# Profession workshops

The production slice contains Skinning, Leatherworking, Mining, Blacksmithing and Enchanting. Skill capacity remains 300; current active gathering and processing steps are:

| Location | Practice range | Entry skill | Next-step skill | Hide |
| --- | --- | --- | --- | --- |
| Whispering Forest | 1–15 | 1 | 16 | Rough |
| Flower Meadow | 16–30 | 16 | 31 | Light |
| Deep Forest | 31–60 | 31 | 61 | Thick |

All current skinnable creatures within a location share its entry and skill-up ceiling. Spiders still give chitin. Reaching 15 alone does not unlock the next zone: a final successful skill increase to 16 completes the step. Skill-up chance follows the existing deterministic declining curve. At 16/31/61 the previous step no longer gives skill, but still yields materials. At 61 the UI explicitly says that the current available ladder is complete; no nonexistent next zone is promised. This is profession skill, not character level or a change to combat encounters.

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

Mining offers copper in Whispering Forest (skill 1), tin in Flower Meadow (16) and iron in Deep Forest (31), following the existing 16/31/61 practice ceilings. Each gathering action yields two ore with a shared 60-second server cooldown. Cooldown is derived from committed mutations as well as profession state, so forgetting/relearning cannot bypass it. The serializable transaction checks the actual location, travel state, skill and inventory capacity before granting materials. Smelting produces copper, bronze and iron bars. Four blacksmith recipes use ordinary level-1/6 equipment budgets and existing item artwork; there are no set bonuses or mass-produced Rare+ items.

Enchanting destroys an unequipped, unlocked Common item for one Strange Dust, or an Uncommon item for one Magic Essence. Other rarities are deliberately unavailable. Three recipes at skills 1/16/31 produce scrolls for +1 Stamina/Strength/Intellect. Applying a scroll requires Enchanting, the city workshop and unequipped chest/gloves/boots; it consumes one scroll and replaces the existing enchantment. Persisted enchantment values are separate from rolled stats, affixes, stars and enhancement. InventorySnapshotReader applies the bonus after structural enhancement and affixes, so the ordinary equipment/combat projection uses it without changing the item's birth roll.

All new mutations use character-scoped intent fingerprints and serializable transactions. Replays return success without granting or consuming anything again. Forgetting a profession requires UI confirmation and resets only its learned skill; items and existing enchantments remain. The two-primary-profession limit is unchanged.

The unified mobile workshop displays all five professions with generated illustrations, trained skills, selected-profession recipes, material guides, mining routes and explicit destructive-action confirmations. Current content ends at skill 61 while overall capacity remains 300. No timed craft queue, global shared veins, Rare+ disenchanting or high-tier enchants are implied.

The EF migration adds nullable mining cooldown and enchantment identity/stat columns plus a zero-default enchantment value. Existing items and professions retain their current behavior. Regression coverage includes parallel gathering, retry replay, forget/relearn cooldown, protected-item destruction, replacement without stat stacking and effective inventory stats.

## City craft district

The city opens a generated illustrated courtyard with four working stations. Leatherworking, Mining (smelting), Blacksmithing and Enchanting each open the matching profession, putting its learning row first if it is not learned. Unlearned stations never show another profession's recipes. Back returns to the district; the district returns to the city. Existing equipment enhancement/reforging remains a separate working service. Mining routes still require the actual outdoor location.

Mining and recipe skill-up rolls use injected server randomness, independent of mutation IDs. Workshop actions reject active travel before consuming materials.

Crafted equipment uses a separate server-generated operation ID for item generation, stable across execution-strategy retries. Client mutation IDs only identify replay requests and cannot select affixes.
