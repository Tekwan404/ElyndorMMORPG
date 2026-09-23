---
name: elyndor-content
description: >
  Elyndor game-content creation, editing, integration, validation, and balance
  skill. Use automatically whenever a task touches mobs, bosses, enemies,
  items, equipment, weapons, armor, loot, drop tables, item sets, locations,
  biomes, dungeons, raids, professions, gathering materials, crafting
  materials, contracts, content JSON, content loaders, content validation,
  rarity, item level, required level, stats, rewards, progression, or balance.
  Also use when converting design documents or spreadsheets into runtime
  content files.
---

# Elyndor Content

## Purpose

Ensure Elyndor content is:

- internally consistent;
- data-driven;
- valid;
- balanced against existing progression;
- compatible with runtime systems;
- maintainable at scale.

Combine this skill with `elyndor-development`.

Use `elyndor-combat` as well when content values affect runtime combat mechanics.

---

# Source of truth

Before adding or changing content:

1. inspect existing content definitions;
2. inspect schemas/models;
3. inspect loaders;
4. inspect validators;
5. inspect IDs/naming conventions;
6. inspect existing examples from the same content category.

Do not invent a parallel JSON shape when the project already has a canonical schema.

---

# Data-driven principle

Content values that designers may reasonably tune should live in content/configuration when the architecture supports it.

Examples:

- enemy stats;
- ability coefficients;
- item stats;
- loot chances;
- boss rewards;
- level requirements;
- location unlock requirements.

Avoid hardcoding balance numbers into runtime C# when they belong in content.

---

# Stable identifiers

Content IDs are persistent contracts.

Use stable machine-readable identifiers.

Prefer formats consistent with the repository, for example:

- `ANCIENT_MINE_BROODMOTHER_L16`
- `DUNGEON_MINES_WARRIOR_LEGENDARY_SHIELD`

Do not casually rename existing IDs if they may already be referenced by:

- player saves;
- inventory rows;
- quest state;
- contracts;
- loot tables;
- admin tools;
- tests;
- migrations.

Changing display names is different from changing stable IDs.

---

# Localization

Separate machine identity from player-facing text.

Check:

- Russian display name;
- description;
- grammar;
- rarity terminology;
- class terminology;
- location terminology.

Do not use the content ID as the displayed name.

If localization infrastructure exists, use it instead of duplicating strings.

---

# Item design

For equipment inspect:

- slot;
- class restrictions;
- level requirement;
- rarity;
- item level/power;
- primary stats;
- defensive stats;
- offensive stats;
- unique effects;
- sell value;
- stack size where applicable.

Do not give every item every stat.

Items should have a readable identity.

---

# Rarity

Rarity should represent more than a color.

Higher rarity can influence:

- stat budget;
- number of useful affixes;
- unique effects;
- drop rarity;
- acquisition difficulty;
- visual prestige.

Do not inflate rarity only by adding tiny +1 values to many unrelated stats.

Prefer coherent stat packages that reinforce an archetype.

---

# Equipment archetypes

When designing class gear, preserve distinct identities.

Examples:

## Warrior

Possible themes:

- durability;
- armor;
- block;
- strength;
- threat;
- melee damage;
- defensive cooldown support.

## Mage

Possible themes:

- spell power;
- mana/resource support;
- elemental specialization;
- critical effects;
- casting utility.

## Archer

Possible themes:

- precision;
- critical chance;
- ranged damage;
- mobility;
- pet/beast synergy where applicable.

## Paladin

Possible themes:

- defense;
- holy/support effects;
- hybrid offensive/defensive stats.

Do not force every class into identical stat distributions.

---

# Item sets

For sets define clearly:

- set identity;
- slots;
- source;
- intended build;
- number of pieces;
- set bonuses if supported.

Set bonuses should encourage an archetype without making every non-set item automatically useless.

Check partial-set and full-set behavior independently.

---

# Stack rules

Stack size should reflect gameplay, not artificial inconvenience.

Common gathering materials should stack generously.

Do not create scarcity by making normal materials unnecessarily awkward to store.

Rare materials can still have high stack sizes; rarity should primarily come from acquisition, not inventory friction.

Use the canonical project stack-size rules.

---

# Loot tables

Every loot table should be understandable.

For each entry identify:

- source;
- item;
- chance/weight;
- min quantity;
- max quantity;
- eligibility requirements if any.

Validate total probability semantics according to the loader implementation.

Do not assume percentages must sum to 100 unless the system actually uses mutually exclusive weighted selection.

---

# Boss loot

Boss rewards should feel meaningfully different from normal mob loot.

Possible boss reward categories:

- guaranteed baseline reward;
- uncommon/rare roll;
- signature equipment;
- crafting component;
- progression key;
- cosmetic/prestige reward where supported.

Avoid a loot table where a boss is merely a normal enemy with more HP.

---

# Dungeon and raid loot

Keep progression source clear.

Ask:

- Is this normal-world gear?
- Dungeon gear?
- Boss-specific gear?
- Raid gear?
- Profession gear?

Do not accidentally make ordinary mobs drop gear intended to define dungeon/raid progression unless explicitly designed that way.

---

# Enemy design

For every enemy inspect:

- level;
- HP;
- attack;
- defense;
- resistance;
- combat role;
- abilities;
- XP;
- loot source;
- location.

Enemies at the same level should not all be identical stat blocks.

Possible roles:

- bruiser;
- tank;
- caster;
- ranged;
- assassin;
- support;
- summoner;
- boss.

Their stats and abilities should communicate their role.

---

# Boss design

Bosses should differ mechanically, not only numerically.

When the runtime supports it, consider:

- phases;
- signature abilities;
- adds;
- AoE;
- enrage;
- control;
- defensive phases;
- target pressure.

Do not design unsupported mechanics into content and then describe them as implemented.

Separate:

- desired design;
- currently supported runtime behavior.

---

# Location progression

Every location should have a reason to exist.

Define:

- level range;
- unlock condition;
- mobs;
- boss;
- resources;
- contracts;
- loot identity;
- connection to other locations.

Avoid overlapping locations with no progression or thematic distinction.

---

# Progression gates

When adding unlock requirements verify they are achievable.

A location must not require:

- an item that cannot drop;
- a boss that cannot spawn;
- a contract that does not exist;
- a level outside reachable progression.

Trace gate dependencies end-to-end.

---

# Dungeons

For dungeon content define:

- level range;
- entrance/unlock;
- encounter list;
- bosses;
- rewards;
- signature loot;
- crafting/profession materials;
- repeatability rules.

Dungeon rewards should be positioned intentionally relative to world content.

---

# Professions

For gathering/crafting content identify:

- profession;
- source location;
- required skill/progression if supported;
- material tier;
- drop/gather rate;
- recipe dependencies;
- final item use.

Never create required crafting materials without a real acquisition source.

Before adding a recipe, prove every ingredient exists and is obtainable.

---

# Missing-content dependency check

Whenever a system references a material, item, mob, location, boss, contract, or loot table:

verify that referenced content exists.

This includes forge/crafting requirements.

Do not allow content like a required "Dungeon Core" to exist only as a recipe dependency with no acquisition path.

For every required content ID answer:

> Where can the player actually obtain this?

If there is no valid answer, the content chain is incomplete.

---

# Contracts

Contracts should point to valid runtime content.

Validate:

- target mob exists;
- location exists;
- level requirement is reasonable;
- kill count is achievable;
- item requirement has a source;
- boss requirement maps to an actual encounter;
- reward exists.

Do not create circular progression where the contract reward is needed to access the contract objective.

---

# Balance methodology

Balance against Elyndor's current numbers first.

External games such as classic MMORPGs may be used as design references, but do not copy their raw numbers blindly.

Use references for concepts such as:

- progression pacing;
- itemization philosophy;
- loot hierarchy;
- rarity;
- dungeon identity;
- profession loops.

Elyndor's actual combat formulas remain authoritative.

---

# Balance workflow

When balancing a level range:

1. establish player baseline stats;
2. establish representative gear;
3. establish expected player damage;
4. establish expected enemy damage;
5. define desired fight duration;
6. derive mob HP/damage;
7. derive boss multiplier;
8. verify rewards;
9. simulate representative builds where tooling exists.

Avoid balancing isolated values independently.

---

# Level progression

Nearby levels should progress smoothly.

Watch for sudden jumps in:

- enemy HP;
- enemy damage;
- armor;
- XP;
- item power;
- required level.

Intentional difficulty spikes should correspond to meaningful events such as:

- boss;
- dungeon;
- new region;
- new tier.

---

# Reward economy

When modifying rewards consider:

- XP/hour;
- gold/hour;
- equipment acquisition;
- crafting material acquisition;
- merchant value;
- repeatability;
- AFK vs manual activity if applicable.

Manual active play should not accidentally become inferior to passive systems unless intentionally designed.

---

# Content validation

After content modifications run available validators.

Validate at minimum:

- duplicate IDs;
- missing references;
- invalid enum values;
- missing required fields;
- negative values;
- impossible level requirements;
- bad loot references;
- invalid item slots;
- class restriction mismatch;
- malformed ranges.

Do not treat valid JSON syntax as sufficient validation.

---

# Runtime compatibility

Whenever introducing a new content field verify:

1. schema/model supports it;
2. loader reads it;
3. validator accepts it;
4. runtime consumes it;
5. admin tools can handle it if applicable;
6. tests cover it.

A field existing only in JSON is not an implemented feature.

---

# Admin tooling

When content is manageable through the admin panel, keep runtime schema and admin capabilities aligned.

If an item supports a stat in JSON but the admin panel cannot create/edit it, treat this as tooling debt.

Do not silently strip unsupported fields during admin editing.

---

# Content file separation

Keep domains logically separated where the repository supports it.

Examples:

- enemies;
- items;
- locations;
- loot;
- professions;
- contracts.

Avoid giant monolithic files if content is already partitioned.

Preserve existing file organization unless there is a clear reason to migrate it.

---

# Review checklist

Before describing content work as complete verify:

- IDs are unique;
- references resolve;
- acquisition sources exist;
- progression gates are achievable;
- numbers fit neighboring content;
- required runtime mechanics actually exist;
- loot is reachable;
- localization is present;
- validators pass;
- relevant tests pass.

---

# Important rule

Never confuse:

"content definition exists"

with:

"gameplay effect is implemented."

If content describes an effect unsupported by runtime code, clearly identify it as not yet implemented rather than pretending the feature works.
