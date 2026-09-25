# Elyndor — Classes & Character Creation

**System:** Classes / Character Creation  
**Status:** Source of Truth  
**Updated:** 2026-09-25

## 1. Playable roster

Current character creation exposes four classes:

| ClassId | Класс | Primary stat | Resource | Основные роли |
| --- | --- | --- | --- | --- |
| `WARRIOR` | Воин | Strength | Rage | Tank / Physical DPS / Party Support |
| `ARCHER` | Лучник | Agility | Focus | Ranged Physical DPS / Pet / Utility |
| `MAGE` | Маг | Intellect | Mana | Ranged Magical DPS / Control |
| `PALADIN` | Паладин | Strength / Intellect by build | Mana | Tank / Melee Holy DPS / Support-Heal |

Future classes such as Priest/Rogue are not part of the current playable roster unless a later Source of Truth change promotes them.

## 2. Character creation

Creation is server-authoritative. The client submits choices; the server validates and persists the character.

Current flow includes:

- class;
- gender/appearance choices supported by current content;
- character name;
- starting class resource/equipment/content derived from authoritative definitions.

Prototype/account rule remains one active character per account unless a later account/character contract explicitly changes it.

Character names must pass authoritative validation and case-insensitive uniqueness rules. UI validation is only early feedback and cannot replace server validation.

## 3. Warrior

`WARRIOR` uses Rage and supports three design directions:

- **Guardian / Страж** — tank, shield, threat and survivability;
- **Berserker / Берсерк** — physical DPS, rage pressure, critical/attack-speed mechanics;
- **Commander / Командир** — party buffs/support/hybrid gameplay.

The design contract contains all three branches. Runtime/content coverage must be checked against current `content/` and tests before claiming a branch is completely playable. As of the current snapshot the dedicated Warrior runtime replacement in active content is centered on Guardian; Berserker/Commander completion remains a concrete content/runtime task.

## 4. Archer

`ARCHER` uses Focus for its primary combat loop. Arcane-oriented mechanics may additionally interact with Mana where defined by the class/resource content.

Talent directions:

- Marksmanship / Меткая стрельба;
- Beast Mastery / Повелитель зверей;
- Survival / Выживание.

Archer owns the companion/pet gameplay integration described by the Companion & Pet system. Pet state, targeting and combat effects remain server-authoritative.

## 5. Mage

`MAGE` uses Mana.

Talent directions:

- Fire / Пламя;
- Arcane / Тайная магия;
- Frost / Лёд.

Mage abilities may use cast timing, direct damage, periodic effects, control and AoE according to Ability/Effect/Damage contracts.

## 6. Paladin

`PALADIN` is a current playable class, not a future placeholder.

Paladin uses Mana and supports holy/melee/defensive/support gameplay according to its current ability/talent content. Character creation, starting state and equipment permissions must be resolved from backend/content definitions rather than frontend class-name special cases.

Paladin appearance assets may have class/gender-specific skins. A missing cosmetic asset may fall back visually, but gameplay identity remains `PALADIN`.

## 7. Appearance and skins

Gameplay class and cosmetic appearance are separate:

- class determines authoritative gameplay permissions/resources/content;
- gender/class compatibility constrains available skins where content says so;
- owned/equipped skin changes presentation only and must not grant combat stats unless an explicit gameplay item/effect contract says otherwise.

## 8. Party and combat identity

Every player actor in combat retains authoritative character/class identity. Party targeting, ally validation, threat, healing and buffs use current CombatSession/Party rules; the frontend cannot promote an arbitrary visible actor to a valid gameplay target.

## 9. Current implementation boundary

As of the 2026-09-25 `main` snapshot:

- Warrior, Archer, Mage and Paladin are available through the current player flow;
- all four have authored ability/talent content in the repository;
- content coverage is not equivalent to full regression coverage;
- the next class-quality milestone is a complete four-class ability/talent gameplay pass rather than adding another class.
