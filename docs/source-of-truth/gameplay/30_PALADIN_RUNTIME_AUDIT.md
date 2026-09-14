# Elyndor — Paladin Runtime Audit

Status: implementation contract for `feat/paladin-talent-rework`.

This document maps the approved Paladin design to the runtime that exists in `main`. It is intentionally conservative: no talent may be reported as implemented when its gameplay effect only exists in text.

## Scope

Paladin has one class identity and three branches:

- `HOLY` — direct-heal healer built around critical heals, Mana efficiency, emergency saves and Beacon;
- `PROTECTION` — active tank built around Holy Shield, Block triggers, retaliation, self-healing, Blessings and party protection;
- `RETRIBUTION` — 2H melee damage dealer built around Seal, Judgement, Crusader Strike, Vengeance, Templar's Verdict and Avenging Wrath.

Talent contract:

- 32 nodes per branch;
- 9 rows;
- gates `0 / 5 / 10 / 15 / 20 / 25 / 30 / 35 / 40`;
- 59 total spendable points;
- hybrid builds are allowed;
- no artificial cross-tree prerequisites.

The data contract lives in `content/talents/paladin.json`.

## Runtime classification rule

The implementation uses the following design classifications:

- `SUPPORTED` — the current runtime already executes the mechanic correctly;
- `EXTEND_EXISTING` — an existing subsystem is the correct owner but needs a Paladin-specific extension;
- `NEW_ABILITY` — an ability definition and its server-authoritative execution path must be added;
- `NEW_SERVER_MECHANIC` — there is no complete existing runtime primitive for the mechanic.

Until a deferred mechanic is implemented, the Paladin talent content keeps an explicit `runtimeStatus: Deferred` marker. Strict talent validation must continue to reject those nodes as merge-ready gameplay.

## Existing primitives we can reuse

### Character stats and Mana — SUPPORTED

The talent runtime already supports generic stat modifiers including:

- `STRENGTH_PERCENT`;
- `INTELLECT_PERCENT`;
- `ARMOR_PERCENT`;
- `ACCURACY_PERCENT`;
- `BLOCK_CHANCE_PERCENT`;
- `BLOCK_VALUE_PERCENT`;
- `MAX_HP_PERCENT`;
- `MAX_RESOURCE_PERCENT`;
- `DAMAGE_DEALT_PERCENT`.

Paladin can use the existing `MANA` resource profile once its class profile is introduced.

### Block core — SUPPORTED / EXTEND_EXISTING

The current runtime already contains `ShieldBlockFormula`, Block Chance / Block Value stats, Guardian block interactions and combat-session hooks. Protection Paladin must reuse those primitives rather than introduce a parallel block engine.

Paladin-specific retaliation, Holy Shield windows, block-trigger healing, cooldown reduction and party shields are still separate extensions.

### Healing amount accounting — EXTEND_EXISTING

`HealingPipeline` already tracks:

- attempted healing;
- effective healing;
- overhealing;
- resulting HP;
- healing-received modifiers.

It does not yet model the full Paladin contract: source actor, healing critical strikes, heal-specific scaling, healing threat and proc metadata. Those must be added before Holy talents such as Illumination, Beacon and crit-trigger effects are marked supported.

### Effects / shields / threat — EXTEND_EXISTING

The project already has an effect engine, shield state, threat table and combat threat layer. They are the correct owners for Paladin effects, but the Paladin behaviours below must be wired explicitly and covered by tests.

## Shared Paladin mechanics

| Mechanic | Classification | Required runtime behaviour |
| --- | --- | --- |
| Mana as the only class resource | `SUPPORTED` after class profile | All three branches use `MANA`; no permanent Faith resource is introduced. |
| Seal state | `NEW_SERVER_MECHANIC` | Exactly one persistent Seal per Paladin; switching replaces the previous Seal. |
| Judgement | `NEW_ABILITY` + `EXTEND_EXISTING` | Active ability resolves an effect from the current Seal and talent modifiers without consuming the Seal. |
| Aura state | `NEW_SERVER_MECHANIC` | Exactly one active Aura per Paladin; party-wide effect; identical Auras from multiple Paladins do not stack. |
| Blessings | `NEW_SERVER_MECHANIC` + `NEW_ABILITY` | Timed ally support effects with server-authoritative cooldowns; not maintenance buffs. |
| Ally targeting | `EXTEND_EXISTING` | Ability execution must safely target party allies and self where allowed. |

## Base kit

The following abilities are required before the class is playable and must be implemented server-authoritatively:

- `HOLY_LIGHT` — `NEW_ABILITY`;
- `FLASH_OF_LIGHT` — `NEW_ABILITY`;
- `JUDGEMENT` — `NEW_ABILITY`;
- `SEAL_OF_RIGHTEOUSNESS` — `NEW_ABILITY` plus persistent Seal state;
- `DEVOTION_AURA` — `NEW_ABILITY` plus Aura state;
- `LAY_ON_HANDS` — `NEW_ABILITY`.

The design document does not define production-ready numerical contracts for every base ability (exact Mana costs, coefficients, cast times, durations and cooldowns). Those values must not be guessed in runtime code. They remain an explicit balance-contract follow-up.

## Holy

| Mechanic | Classification | Notes |
| --- | --- | --- |
| Holy/Flash direct-heal modifiers | `EXTEND_EXISTING` | Heal-specific ability modifiers and scaling. |
| Healing critical strikes | `EXTEND_EXISTING` | Required for Illumination, Divine Favor, Holy Power, Surge of Light and Afterglow. |
| Illumination Mana refund | `NEW_SERVER_MECHANIC` | Refund is based on Mana actually spent by the critical heal. |
| Divine Favor | `NEW_ABILITY` | Off-GCD; next direct heal is forced crit; must be consumed exactly once. |
| Cleanse | `NEW_ABILITY` | Removes one eligible negative effect from an ally. |
| Holy Shock | `NEW_ABILITY` | Dual-purpose ally heal / enemy Holy damage. |
| Beacon of Light | `NEW_SERVER_MECHANIC` + `NEW_ABILITY` | Server-side copied healing with recursion protection. |
| Beacon recursion guards | `NEW_SERVER_MECHANIC` | No Beacon -> Beacon recursion, proc loops, copy-of-copy or HoT recursion. |
| Concentration Aura | `NEW_ABILITY` + Aura runtime | Party cast-time modifier; duplicate aura does not stack. |
| Afterglow | `NEW_SERVER_MECHANIC` | Heal-over-time derived from effective crit healing; cannot crit or trigger Illumination. |
| Divine Replenishment | `NEW_ABILITY` | Mana recovery over time with temporary healing-throughput penalty. |
| Aura Mastery | `NEW_ABILITY` | Temporarily amplifies current Aura and adds group defensive benefit. |
| Herald of Light counter | `NEW_SERVER_MECHANIC` | Every third eligible successful direct heal resets Holy Shock and makes the next Holy Shock free; copied/HoT/secondary healing cannot increment the counter. |

## Protection

| Mechanic | Classification | Notes |
| --- | --- | --- |
| Armor / Block Value / Accuracy / Max HP stat nodes | `SUPPORTED` | Generic stat runtime is reusable. |
| Righteous Fury | `EXTEND_EXISTING` | Tank stance, Holy-threat multiplier and incoming-damage modifier. |
| Consecration | `NEW_ABILITY` + `EXTEND_EXISTING` | Encounter-wide Holy pulses, not positional ground targeting. |
| Holy Shield | `NEW_ABILITY` + `EXTEND_EXISTING` | Timed Block Chance window plus Holy retaliation on successful Block. |
| Reckoning | `NEW_SERVER_MECHANIC` | Block proc arms the next autoattack extra hit. |
| Judgement of Justice | `EXTEND_EXISTING` | Attack-speed debuff; boss-safe reduced version instead of dead hard CC. |
| Avenger's Shield | `NEW_ABILITY` | Ranged multi-target Holy shield attack with high Threat. |
| Ardent Defender | `EXTEND_EXISTING` | HP-threshold conditional incoming-damage reduction. |
| Hammer of the Righteous | `NEW_ABILITY` | Shield-based Holy attack scaling from defensive characteristics. |
| Divine Protection | `NEW_ABILITY` | Strong personal defensive window. |
| Intercession | `NEW_SERVER_MECHANIC` | Redirect a portion of ally damage to Paladin after the ally's mitigation; recursion and lethal-edge cases must be defined and tested. |
| Hammer of Justice | `NEW_ABILITY` | Interrupt everywhere; hard stun only where target rules permit it. |
| Unbreakable Bastion | `NEW_SERVER_MECHANIC` | Large-hit reducer with internal cooldown. |
| Divine Bastion / Bastion of Dawn party shields | `NEW_SERVER_MECHANIC` | Server-owned shields applied to party members; ICD and target selection required. |

## Retribution

| Mechanic | Classification | Notes |
| --- | --- | --- |
| Strength / generic damage nodes | `SUPPORTED` | Existing stat modifier runtime is reusable. |
| Seal of Command | `NEW_ABILITY` + Seal runtime | Persistent Seal; autoattack proc performs additional Holy hit. |
| Crusader Strike | `NEW_ABILITY` | Main weapon-based rotational strike. |
| Vengeance | `NEW_SERVER_MECHANIC` | Physical/Holy crits build up to three timed stacks; crit refreshes duration. |
| Offensive Consecration | `NEW_ABILITY` + `EXTEND_EXISTING` | Encounter-wide periodic Holy damage. |
| Sanctity Aura | `NEW_ABILITY` + Aura runtime | Personal Holy bonus plus small party offensive bonus; duplicates do not stack. |
| Repentance | `NEW_ABILITY` | Hard incapacitate for ordinary targets; boss-safe damage-dealt reduction replacement. |
| Templar's Verdict | `NEW_ABILITY` | Heavy 2H finishing strike with high Mana cost and weapon scaling. |
| Blessing of Might | `NEW_ABILITY` + Blessing runtime | Timed Attack Power boost for self or ally. |
| Art of War | `NEW_SERVER_MECHANIC` | Physical crit modifies next Flash of Light; rank 2 instant-cast/cost override. |
| Divine Storm | `NEW_ABILITY` + `EXTEND_EXISTING` | Encounter-wide damage plus bounded group healing conversion. |
| Sanctified Judgement | `EXTEND_EXISTING` | Judgement returns Mana. |
| Avenging Wrath | `NEW_ABILITY` | Major timed damage cooldown. |
| Execute modifier | `EXTEND_EXISTING` | Templar's Verdict bonus below target HP threshold. |
| Divine Purpose counter | `NEW_SERVER_MECHANIC` | Every third successful Judgement empowers the next Templar's Verdict exactly once. |
| Incarnation of Retribution | `NEW_SERVER_MECHANIC` + `EXTEND_EXISTING` | Longer Avenging Wrath; first Verdict guaranteed crit; first Judgement resets Crusader Strike cooldown. |

## Missing numerical contracts — do not guess

The approved design intentionally uses qualitative phrases in several places, for example “significantly”, “small amount”, “for several seconds”, “large cooldown” and “small defensive bonus”. Before the corresponding runtime can become `SUPPORTED`, content must define the production values needed by the engine, including where applicable:

- Mana cost;
- base amount / weapon coefficient / spell coefficient;
- cast time;
- cooldown;
- duration;
- proc chance;
- internal cooldown;
- threat multiplier / flat threat;
- damage-reduction percentage;
- shield amount or scaling;
- healing-conversion percentage;
- party aura magnitude.

No implementation should substitute arbitrary numbers for those missing contracts.

## Merge-readiness gates

The Paladin change is ready to merge only when all of the following are true:

1. `PALADIN` class profile exists with approved stats, equipment permissions, resource profile and base ability grants.
2. Required Paladin abilities exist in content and pass content validation.
3. Every one of the 96 talents has an executable gameplay modifier; no `Deferred` modifier remains.
4. Holy crit/effective-heal/overheal/Mana-refund tests pass.
5. Beacon recursion/copy-of-copy/HoT recursion tests pass.
6. Seal switching and Judgement resolution tests pass.
7. Aura one-active and duplicate-nonstacking tests pass.
8. Blessing cooldown/expiry/target tests pass.
9. Protection Block retaliation, redirect, party shield and boss-safe CC tests pass.
10. Retribution Vengeance, counters, cooldown reset, execute and Divine Storm tests pass.
11. Full content validator, backend build/test and required CI are green.

Until these gates are satisfied the branch is an implementation workstream, not a merge-ready class release.
