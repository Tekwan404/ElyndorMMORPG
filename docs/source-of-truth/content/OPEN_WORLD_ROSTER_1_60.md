# Elyndor — Open World Roster 1–60

Status: working source of truth for open-world content curation.

## Core rule

Open-world roster design is **monster-first and art-first**.

If an existing monster has a good dedicated art asset and the monster is redundant in its current location, move the **monster identity** to a better-fitting location when practical. Do not silently detach its art and assign that art to an unrelated monster.

An active production monster should converge on:

```
Monster identity
  -> exact art
  -> location role
  -> AI role
  -> abilities
  -> thematic loot
  -> tuned stats
```

Semantic/location art fallback is a client safety net. It is not considered finished art coverage.

Dungeons are curated separately. Open-world cleanup must not change dungeon encounter rosters unless the dungeon itself is explicitly in scope.

## Roster density

A normal open-world location should usually contain **5–7 active monster identities**.

The count may exceed 7 when the location already has several strong, distinct, art-backed monsters. Do not delete a useful art-backed identity just to satisfy a numeric cap.

Each location should prefer distinct gameplay roles over name variants of the same stat block.

## Current 1–40 target after art-first cleanup

| Location | Level band | Active roster | Exact-art baseline |
| --- | ---: | ---: | ---: |
| Whispering Forest | 1–5 | 7 | 7 |
| Flower Meadow | 1–8 | 6 | 2 |
| Deep Forest | 1–11 | 6 | 6 |
| Old Road | 9–14 | 6 | 1 |
| Stone Spurs | 12–16 | 6 | 1 |
| Blighted Grove | 15–20 | 13 | 13 |
| Ashen Border | 18–23 | 6 | 3 |
| Moon-Ash Marshes | 21–26 | 7 | 5 |
| Eclipse Outskirts | 24–29 | 6 | 0 |
| Shattered Lands | 27–32 | 6 | 6 |
| Blackstone Highlands | 30–35 | 6 | 0 |
| Crimson Wasteland | 33–38 | 7 | 0 |
| Obsidian Edge | 36–40 | 7 | 0 |

Current baseline: **89 active open-world encounters, at least 44 with exact monster art**.

Blighted Grove intentionally exceeds the normal density target for now because it contains many distinct finished monster identities. Later redistribution is allowed, but deleting those identities solely to reduce the number is not.

## 41–60 progression structure

The current runtime world ends at level 40. Levels 41–60 must be introduced as a new open-world content block instead of stretching existing 1–40 locations.

Use five overlapping progression bands:

| Band | Recommended range | Target roster |
| --- | ---: | ---: |
| A | 41–44 | 6–7 |
| B | 44–48 | 6–7 |
| C | 48–52 | 6–7 |
| D | 52–56 | 6–7 |
| E | 56–60 | 6–8 |

Final location names and visual themes are not locked until their art direction is selected.

A 41–60 location must not be enabled in production until it has:

- a location art direction;
- at least 5 exact monster arts;
- a coherent 5–8 monster roster;
- thematic loot families;
- one clear gameplay role per normal monster;
- at least one elite identity where appropriate;
- level-appropriate stats and rewards;
- reachable quest/content hooks if the zone participates in progression.

## Monster role budget

A normal monster should normally have one readable mechanic.

Recommended reusable roles:

- Beast / fast pressure;
- Bruiser / heavy hit;
- Tank / high armor or mitigation;
- Archer / ranged-flavoured damage pattern;
- Caster / interruptible cast;
- Support / buff, debuff, heal or summon;
- Assassin / burst or execute pressure;
- Elite / 2–3 mechanics.

Do not create multiple monsters whose only practical difference is name, HP and attack power.

## AI budget

Normal:
- auto attack;
- 1 defining ability;
- optional simple passive/debuff.

Elite:
- 2–3 defining actions;
- one clear priority/condition.

Boss:
- handled by boss/dungeon/world-boss design, not by normal field-roster quotas.

PvE AI must use the normal Combat/Ability/Effect systems. No per-monster second combat engine.

## Loot rule

Each active monster should have a readable reason to farm it.

Preferred structure:

```
common thematic material
+ uncommon thematic trophy/resource
+ optional designated equipment chase drop
+ gold through goldRewardMin/goldRewardMax
```

Examples:

- wolf -> hide / fang;
- spider -> silk / venom;
- humanoid bandit -> cloth / weapon parts;
- construct -> stone / rune fragments;
- undead -> bone / soul / cursed reagent;
- caster -> magical reagent;
- elite -> specific rare trophy or equipment.

Do not use coin-bag inventory items when the monster already awards gold directly.

Do not give animal meat/hide loot to humanoids, cloth/weapon-junk loot to plants, or meat/hide loot to undead merely because a shared table was convenient.

## Art acceptance rule

For new 41–60 content, semantic fallback does not count toward the art budget.

Production-ready target:

```
active monster id
  -> artId
  -> exact asset file
```

Fallback remains only for defensive rendering.

## Implementation order

1. Select or create exact monster art.
2. Create/keep the monster identity.
3. Place the monster into the best-fitting location.
4. Assign role and AI.
5. Assign thematic loot.
6. Tune HP, damage and defenses.
7. Add quest/progression references.
8. Validate content and regression coverage.

Do not start by generating dozens of names and stat blocks.
