# Skinning and leatherworking workshop

The production slice contains Skinning and Leatherworking, not the three proposed professions in the original crafting foundation. Skill capacity remains 300; current active gathering and processing steps are:

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

New professions and further gathering tiers are separate future slices. Raising current gathering/recipe ceilings to 300 without new meaningful sources and products is not part of this release.
