# New Pictures Content Import Manifest

**Branch:** `feat/new-pictures-content-refresh`
**Source:** `C:\Users\tekwan\Downloads\ELYNDOR\pic\new pictures`
**Purpose:** auditable mapping from authored bundles to the existing Elyndor runtime content package.

## Source inventory

| Source file/category | Records/files | Intended current category |
| --- | ---: | --- |
| `json/Elyndor_Items_Loot_Professions_1-40.json:items` | 688 | `content/items/*.json` |
| `json/Elyndor_Items_Loot_Professions_1-40.json:equipmentSets` | 60 | `content/sets/*.json` |
| `json/Elyndor_Items_Loot_Professions_1-40.json:lootTables` | 227 | `content/loot/*.json` |
| `json/Elyndor_Items_Loot_Professions_1-40.json:professionZoneProfiles` | 39 | Existing `Professions`/`SkinningSources`/`ProfessionRecipes` only; general zone profiles have no direct runtime model |
| `json/Elyndor_Locations_1-40.json:locations` | 21 | `content/package.json` / `content/locations/*.json` |
| `json/Elyndor_Locations_1-40.json:professionZoneProfiles` | 39 | Existing profession source/recipe models only; gathering zone rolls have no direct runtime model |
| `json/Elyndor_Mobs_1-40.json:monsters` | 227 | `content/monsters/*.json` and location encounter fragments |
| `json/Elyndor_Mobs_1-40.json:monsterAiProfiles` | 6 | `content/monsters/*.json` |
| `item set/` | 60 sheets | `web/elyndor-web/src/assets/items/sets/` (crop map requires visual audit) |
| `Предметы вне комплектов/` | 8 sheets | `web/elyndor-web/src/assets/items/` (crop map requires visual audit) |
| `Враги/` | 51 images | `web/elyndor-web/src/assets/monsters/` (needs stable `artId` mapping) |
| `Материал профессий/` | 10 sheets | item/material art under existing assets (crop map requires visual audit) |
| `Материалы, не используемых в профессиях/` | 3 images | item/material art under existing assets |
| `Фоны/` | 18 images | `web/elyndor-web/src/assets/world/` (needs location mapping) |
| `Карта мира.png` | 1055×1491 px | existing world-map UI asset/helper |
| `md/` | 3 files | authoring/design references, not runtime input |

The JSON source bundles carry extra wrapper/editorial fields (`bundleType`,
`runtimeSplitTargets`, `sourceDocuments`, `designRules`, and `compatibility`). These
are not copied into runtime fragments unless an existing package contract explicitly
defines the equivalent.

The implemented slice selects the 13 non-instance field locations from
`WHISPERING_FOREST` through `OBSIDIAN_EDGE`, their 134 authored Normal-rank
encounters, 134 direct loot tables, and 121 supported Material/Consumable item
definitions. Existing instance locations and `STARTER_TOWN` are preserved. The 35
authored Elite roster entries are not placed into location encounter pools because
the current encounter validator only permits Normal entries or a sole Boss entry;
elite encounter/spawn rules need a separate supported content contract.

## Existing runtime contracts inspected

- `CategoryContentComposer` composes category JSON into `GameContentPackage`; it
  supports items, equipment sets, loot tables, locations, monsters and AI profiles.
- Its `ContentCategoryFragment` and `LocationEncounterFragment` records are private
  implementation details. Import tooling must emit their existing JSON shape; it
  must not add a second runtime loader or expose these types just for conversion.
- `ItemDefinition` supports stable ID, icon ID, type/rarity/level/stacking, slot,
  primary stats, set ID, weapon/armor/offhand categories, block values, roll ranges,
  affix pool/count/policy, trade policy, item level, and premium eligibility.
- `EquipmentSetDefinition` supports ID/name and stat-based bonuses. It has no
  `classId`, `branchId`, `pieceItemIds`, arbitrary effect, or design-only bonus field.
- `LootTableDefinition` is a list of item entries with drop chance and quantity
  bounds. It has no `mode` or `randomEquipmentProfile` field.
- `LocationDefinition` supports transitions, recommended/min/max levels, contract,
  art ID, description, travel duration and AFK availability. Location encounter
  rosters are separate `LocationEncounterFragment`s.
- `MonsterDefinition` and `MonsterAiProfile` support the source's core combat fields
  and XP/gold/loot/art data, subject to canonical validator limits.
- Profession runtime data is `Professions`, `SkinningSources`, and
  `ProfessionRecipes` in `GameContentPackage`; source `professionZoneProfiles` with
  free-form `rawRule` and per-zone rolls has no one-to-one runtime representation.
  Skinning/recipe authoring can map to existing models, but the general gather-zone
  system cannot. Do not discard or reinterpret these 39 profiles silently.

## Mapping and loss policy

1. Preserve every authored stable ID wherever it does not collide with an existing
   ID or violate a current contract.
2. Map source fields by name/meaning only when an equivalent runtime property exists.
   `designEligibleClasses`, `designContentId`, `designDropSource`, and
   `designDropChanceOrWeight` are authoring metadata, not authoritative runtime rules.
3. Convert source location `encounters` into one existing location encounter fragment
   per location, and ensure every referenced monster ID is present.
4. Convert item loot rows to canonical `LootTableEntry` fields only after verifying
   chance/weight semantics, stack quantity bounds, and item references.
5. Convert set bonuses only when they are representable by the existing stat bonus
   contract; do not claim runtime behavior for unsupported effects.
6. Treat every unrepresentable field/reference as an import error or a named review
   finding. Never silently ignore an authored gameplay rule.
7. Item definitions classified as materials are inventory content and can be imported
   through the existing item contract. Actual gathering nodes/zone harvest rolls are
   not inferred from artwork or free-form `rawRule` text.

## Known review findings before content replacement

- Runtime item/set/location/monster contracts differ from the richer source format;
  an explicit converter and validator report are required.
- The 39 profession-zone profiles require a field-by-field assessment. Existing
  skinning sources and profession recipes may represent some outcomes, but the
  source's general gathering-zone profiles do not currently have a matching runtime
  entity.
- Source set definitions include design-only fields and must be reduced to runtime
  stat bonuses without changing their authored ID/name or referencing missing pieces.
- Source loot tables may include mode/random-equipment profile metadata not represented
  by the current deterministic table entry contract. Preserve only semantics the
  runtime actually implements; flag unsupported records instead of guessing.
- Existing locations, encounters, quests, dungeons, contracts, merchants, recipes,
  AFK eligibility, and starter-town references must be mapped before any old
  definitions are removed.
- Fifteen exact stable-ID collisions exist between source and checked-in content:
  `BOAR_TUSK`, `CHITIN_FRAGMENT`, `LIGHT_HIDE`, `SPIDER_SILK`,
  `SPIDER_VENOM_SAC`, `THICK_HIDE`, `WOLF_FANG`, `STARTER_TOWN`,
  `WHISPERING_FOREST`, `DEEP_FOREST`, `BLIGHTED_GROVE`, `BROODMOTHER_LAIR`,
  `ANCIENT_MINE`, `ECLIPSED_CITADEL`, and `SHATTERED_ORDER_CITADEL_TEST`.
  These are expected replacement/compatibility candidates, not safe to overwrite
  until references and semantic differences are reviewed.
- The authored field-zone rosters reference legacy monster IDs absent from the new
  mob bundle. The import replaces those encounter aliases with the authored roster;
  existing legacy monster definitions are retained if another system references
  them.
- The JSON location bundle also includes instance entries. The companion design
  explicitly marks level 20/30/40 content as raids. Exclude those raid instances and
  the level-40 `BLACK_BASTION` portal transition; preserve existing dungeon/instance
  definitions and do not create new raid definitions, loot, or boss encounters.
- All 169 selected field-mob loot tables include `randomEquipmentProfile`, while the
  current `LootTableDefinition` supports direct independent item entries only. This
  slice imports and validates the direct entries; randomized bonus-equipment rolls
  remain unimplemented and must not be described as working.
- The selected field rosters contain 134 normal and 35 elite mobs; this slice imports
  the 134 normal encounters only. The current location encounter validator rejects
  Elite entries in ordinary encounter pools. No boss rank is included.
- Of the direct-loot item definitions referenced by the selected tables, 26
  Equipment definitions currently fail runtime category/slot or procedural
  itemization validation, and 3 `Recipe` definitions have no corresponding
  `ItemType`. Those entries are omitted from the direct tables rather than remapped
  to incorrect types. Every imported table retains at least one supported drop.
- All imported direct loot tables omit the source `randomEquipmentProfile`: the
  runtime only supports explicit `LootTableEntry` rolls. Random bonus equipment is
  not active in this slice.
- Source location danger `DEADLY` is mapped to the supported runtime danger
  `DANGEROUS`; numeric recommended/min/max levels remain authored. Raid IDs are
  removed from imported field-location transitions.
- The reset's starter hub ID, item-instance references, active grants, and pending
  loot policy must be verified from the final package and persistence graph. No
  production database operation is part of this import.

## Preservation checks

Before the art import, record checksums of existing talent, ability, and character
assets. After import, compare those scoped asset trees and confirm there are no
changes. Content replacement is limited to explicitly mapped authored categories;
it does not delete class/talent/ability/character definitions or unrelated mechanics.

Baseline aggregate SHA-256 values (sorted relative path + per-file SHA-256 rows):

| Existing art tree | Files | Aggregate SHA-256 |
| --- | ---: | --- |
| `web/elyndor-web/src/assets/game/talents/` | 409 | `83FD2315D15D427FC9BE05C0F762FC2C7A21087186B99E0BAFFCDC54FA3C93F6` |
| `web/elyndor-web/src/assets/abilities/` | 104 | `502CB48DF9BD7B1F04BAEA6C4F4333001A6109DC2DD774B1AAF3527D8BBEDBBA` |
| `web/elyndor-web/src/assets/characters/` | 13 | `16E690A8DB89E57B54A339F5BF1B386BDE6F59A77D34AEE93AB002A56B0ADED3` |
