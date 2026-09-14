# Elyndor — Paladin Runtime Audit

Status: active implementation contract for `feat/paladin-talent-rework`.

This document maps the approved Paladin design to the runtime that exists in the feature branch. It is intentionally conservative: no talent may be reported as implemented when its gameplay effect only exists in text or when a reusable runtime primitive has not yet been connected to `CombatSession`.

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

- `SUPPORTED` — the current runtime already executes the mechanic correctly end to end;
- `FOUNDATION_READY` — the reusable server primitive and regression coverage exist, but Paladin ability/session integration is not complete yet;
- `EXTEND_EXISTING` — an existing subsystem is the correct owner but needs a Paladin-specific extension;
- `NEW_ABILITY` — an ability definition and its server-authoritative execution path must be added;
- `NEW_SERVER_MECHANIC` — there is no complete existing runtime primitive for the mechanic.

Until a deferred mechanic is implemented end to end, the Paladin talent content keeps an explicit `runtimeStatus: Deferred` marker. Strict talent validation must continue to reject those nodes as merge-ready gameplay.

## Implementation progress in this branch

The following runtime foundation is already present in code and covered by focused unit tests:

- `HealingPipeline` now carries source actor, effective healing, overheal, Spell Power scaling, opt-in healing criticals resolved through injected server RNG, forced criticals and healing origin metadata;
- `AbilityEngine` now routes healing actions through that source-aware pipeline, including `SpellPowerCoefficient`, `CanCrit` and ability/target critical modifiers;
- `CombatEvent` exposes healing `IsCritical` and `HealingOrigin` so class runtimes can distinguish direct healing from periodic, copied and secondary healing;
- `PaladinCombatState` enforces one active Seal, one active Aura and one Beacon target per Paladin;
- Beacon eligibility accepts only effective `Direct` healing applied to another target, so copied, periodic and secondary healing cannot recursively feed Beacon;
- `PaladinHealingRuntime` provides one-shot Divine Favor state, Illumination refund calculation from Mana actually spent, Afterglow calculation from effective direct critical healing and the Herald of Light every-third-direct-heal counter;
- `PaladinProtectionRuntime` provides Holy Shield / Consecration windows, the exact Ardent Defender `<35% HP` gate, Consecrated Protection eligibility and post-mitigation Intercession redirect calculation;
- `PaladinRetributionRuntime` provides Vengeance state capped at three stacks with duration refresh, Zeal arming, Divine Purpose every-third-Judgement state and one-shot Incarnation of Retribution effects inside Avenging Wrath;
- content validation now enforces the Paladin tree shape: exactly 96 nodes, three branches and 32 nodes per branch.

These primitives intentionally do not invent missing balance values. They become `SUPPORTED` only after they are connected to actual Paladin ability execution and talent hooks.

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

Paladin-specific retaliation, Holy Shield block consequences, cooldown reduction and party shields still need CombatSession wiring.

### Healing amount accounting — FOUNDATION_READY

`HealingPipeline` now tracks:

- source and target actor;
- attempted healing;
- Spell Power scaling;
- opt-in critical healing using injected server RNG;
- forced critical healing for effects such as Divine Favor;
- modified healing;
- effective healing;
- overhealing;
- resulting HP;
- healing-received modifiers;
- `Direct / Periodic / Copied / Secondary` origin;
- critical/origin metadata on the emitted `HealingApplied` event.

Remaining shared extension: healing threat has not yet been added to the threat layer, and CombatSession still needs Paladin-specific processing of those healing events.

### Effects / shields / threat — EXTEND_EXISTING

The project already has an effect engine, shield state, threat table and combat threat layer. They are the correct owners for Paladin effects, but the Paladin behaviours below must be wired explicitly and covered by tests.

## Shared Paladin mechanics

| Mechanic | Classification | Required runtime behaviour |
| --- | --- | --- |
| Mana as the only class resource | `SUPPORTED` after class profile | All three branches use `MANA`; no permanent Faith resource is introduced. |
| Seal state | `FOUNDATION_READY` | `PaladinCombatState` enforces exactly one Seal and replacement semantics. Ability/session integration is still required. |
| Judgement | `NEW_ABILITY` + `EXTEND_EXISTING` | Active ability resolves an effect from the current Seal and talent modifiers without consuming the Seal. |
| Aura state | `FOUNDATION_READY` | One Aura per Paladin is enforced locally. Party-wide application and duplicate-aura non-stacking across multiple Paladins still need party/session integration. |
| Blessings | `NEW_SERVER_MECHANIC` + `NEW_ABILITY` | Timed ally support effects with server-authoritative cooldowns; not maintenance buffs. |
| Ally targeting | `EXTEND_EXISTING` | `AbilityEngine` has `SingleAlly`, but party-membership validation must be authoritative rather than accepting any non-self actor. |

## Base kit

The following abilities are required before the class is playable and must be implemented server-authoritatively:

- `HOLY_LIGHT` — `NEW_ABILITY`;
- `FLASH_OF_LIGHT` — `NEW_ABILITY`;
- `JUDGEMENT` — `NEW_ABILITY`;
- `SEAL_OF_RIGHTEOUSNESS` — `NEW_ABILITY` plus Seal state integration;
- `DEVOTION_AURA` — `NEW_ABILITY` plus Aura/party state integration;
- `LAY_ON_HANDS` — `NEW_ABILITY`.

The design document does not define production-ready numerical contracts for every base ability (exact Mana costs, coefficients, cast times, durations and cooldowns). Those values must not be guessed in runtime code. They remain an explicit balance-contract follow-up.

## Holy

| Mechanic | Classification | Notes |
| --- | --- | --- |
| Holy/Flash direct-heal modifiers | `FOUNDATION_READY` / `EXTEND_EXISTING` | Generic healing actions now support source Spell Power and crit; ability-specific modifiers still need Paladin wiring. |
| Healing critical strikes | `FOUNDATION_READY` | Server-authoritative opt-in crit path exists and emits crit metadata. |
| Illumination Mana refund | `FOUNDATION_READY` | Runtime calculation uses Mana actually spent and only accepts direct critical healing; CombatSession refund hook remains. |
| Divine Favor | `FOUNDATION_READY` + `NEW_ABILITY` | One-shot forced-crit state exists; the actual off-GCD ability and consumption hook remain. |
| Cleanse | `NEW_ABILITY` | Removes one eligible negative effect from an ally. |
| Holy Shock | `NEW_ABILITY` | Dual-purpose ally heal / enemy Holy damage. |
| Beacon of Light | `FOUNDATION_READY` + `NEW_ABILITY` | Single Beacon target and direct-heal eligibility exist; actual copied-heal execution remains. |
| Beacon recursion guards | `FOUNDATION_READY` | Copied, periodic and secondary healing are ineligible for Beacon copy, preventing copy-of-copy/HoT recursion at the state boundary. |
| Concentration Aura | `NEW_ABILITY` + Aura runtime | Party cast-time modifier; duplicate aura does not stack. |
| Afterglow | `FOUNDATION_READY` | Total HoT amount derives from effective direct critical healing; actual timed HoT application remains. |
| Divine Replenishment | `NEW_ABILITY` | Mana recovery over time with temporary healing-throughput penalty. |
| Aura Mastery | `NEW_ABILITY` | Temporarily amplifies current Aura and adds group defensive benefit. |
| Herald of Light counter | `FOUNDATION_READY` | Only effective direct healing counts; every third eligible heal arms the free Holy Shock state. Cooldown-reset integration remains. |

## Protection

| Mechanic | Classification | Notes |
| --- | --- | --- |
| Armor / Block Value / Accuracy / Max HP stat nodes | `SUPPORTED` | Generic stat runtime is reusable. |
| Righteous Fury | `EXTEND_EXISTING` | Tank stance, Holy-threat multiplier and incoming-damage modifier. |
| Consecration | `NEW_ABILITY` + `FOUNDATION_READY` | Runtime can track its active window; encounter-wide Holy pulses and Threat remain. |
| Holy Shield | `NEW_ABILITY` + `FOUNDATION_READY` | Runtime tracks the defensive window and block-trigger eligibility; Block Chance buff and retaliation execution remain. |
| Reckoning | `NEW_SERVER_MECHANIC` | Block proc arms the next autoattack extra hit. Proc chance is not numerically specified yet. |
| Judgement of Justice | `EXTEND_EXISTING` | Attack-speed debuff; boss-safe reduced version instead of dead hard CC. |
| Avenger's Shield | `NEW_ABILITY` | Ranged multi-target Holy shield attack with high Threat. |
| Ardent Defender | `FOUNDATION_READY` | Exact `<35% HP` eligibility and configured reduction resolution exist; damage pipeline/session integration remains. |
| Hammer of the Righteous | `NEW_ABILITY` | Shield-based Holy attack scaling from defensive characteristics. |
| Divine Protection | `NEW_ABILITY` | Strong personal defensive window. |
| Intercession | `FOUNDATION_READY` / `NEW_SERVER_MECHANIC` | Post-mitigation redirect math is isolated and validated; redirect ownership, recursion and lethal-edge processing still need session integration. |
| Hammer of Justice | `NEW_ABILITY` | Interrupt everywhere; hard stun only where target rules permit it. |
| Unbreakable Bastion | `NEW_SERVER_MECHANIC` | Large-hit reducer with internal cooldown. |
| Divine Bastion / Bastion of Dawn party shields | `NEW_SERVER_MECHANIC` | Server-owned shields applied to party members; ICD and target selection required. |

## Retribution

| Mechanic | Classification | Notes |
| --- | --- | --- |
| Strength / generic damage nodes | `SUPPORTED` | Existing stat modifier runtime is reusable. |
| Seal of Command | `NEW_ABILITY` + Seal runtime | Persistent Seal; autoattack proc performs additional Holy hit. |
| Crusader Strike | `NEW_ABILITY` | Main weapon-based rotational strike. |
| Vengeance | `FOUNDATION_READY` | Runtime caps at three stacks and refreshes duration on eligible crit; damage-bonus magnitude/duration still come from content. |
| Offensive Consecration | `NEW_ABILITY` + `EXTEND_EXISTING` | Encounter-wide periodic Holy damage. |
| Sanctity Aura | `NEW_ABILITY` + Aura runtime | Personal Holy bonus plus small party offensive bonus; duplicates do not stack. |
| Repentance | `NEW_ABILITY` | Hard incapacitate for ordinary targets; boss-safe damage-dealt reduction replacement. |
| Zeal | `FOUNDATION_READY` | Successful Judgement arms one next-autoattack state; configured extra-damage magnitude remains content-owned. |
| Templar's Verdict | `NEW_ABILITY` | Heavy 2H finishing strike with high Mana cost and weapon scaling. |
| Blessing of Might | `NEW_ABILITY` + Blessing runtime | Timed Attack Power boost for self or ally. |
| Art of War | `NEW_SERVER_MECHANIC` | Physical crit modifies next Flash of Light; rank 2 instant-cast/cost override. |
| Divine Storm | `NEW_ABILITY` + `EXTEND_EXISTING` | Encounter-wide damage plus bounded group healing conversion. |
| Sanctified Judgement | `EXTEND_EXISTING` | Judgement returns Mana. |
| Avenging Wrath | `FOUNDATION_READY` + `NEW_ABILITY` | Timed window state exists; actual damage buff ability remains. |
| Execute modifier | `EXTEND_EXISTING` | Templar's Verdict bonus below target HP threshold. |
| Divine Purpose counter | `FOUNDATION_READY` | Every third successful Judgement arms the next Verdict exactly once. |
| Incarnation of Retribution | `FOUNDATION_READY` / `EXTEND_EXISTING` | First Verdict crit and first Judgement Crusader-reset are independently one-shot inside Avenging Wrath; duration extension and cooldown mutation remain. |

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
4. Holy crit/effective-heal/overheal/Mana-refund tests pass in CI and the primitives are wired to CombatSession.
5. Beacon actual copied healing passes recursion/copy-of-copy/HoT recursion tests.
6. Seal switching and Judgement resolution pass end-to-end tests.
7. Aura one-active and duplicate-nonstacking across multiple Paladins pass end-to-end tests.
8. Blessing cooldown/expiry/target tests pass.
9. Protection Block retaliation, redirect, party shield and boss-safe CC tests pass.
10. Retribution Vengeance, counters, cooldown reset, execute and Divine Storm tests pass end to end.
11. Full content validator, backend build/test and required CI are green.

Until these gates are satisfied the branch is an implementation workstream, not a merge-ready class release.
