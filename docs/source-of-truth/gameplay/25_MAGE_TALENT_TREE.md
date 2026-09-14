# Elyndor — Mage Talent Tree — Source of Truth

**Document:** `docs/source-of-truth/gameplay/25_MAGE_TALENT_TREE.md`  
**Class:** Mage (`MAGE`)  
**Level Cap:** 60  
**Talent Points:** 59  
**Primary Attribute:** Intellect  
**Action Resource:** Mana  
**Armor:** Cloth  
**Weapons:** Staff / Wand  
**Branches:** Пламя / Тайная магия / Лёд  
**Tree:** `MAGE_TREE`, version 2

This document supersedes the legacy Arcane Charges / Frostbite-stack / Fire Comet design.

Exact production ranks, prerequisites and numeric modifier values live in `content/talents/mage-pyromancer.json`; ability definitions live in `content/abilities/mage-pyromancer.json`. This document defines the gameplay/runtime contract those values implement.

---

# 1. Общий каркас

Mage keeps one shared talent tree with three hybrid-compatible branches:

- `FIRE` — Пламя;
- `ARCANE` — Тайная магия;
- `FROST` — Лёд.

Hard requirements:

- exactly **32 talents per branch**, **96 total**;
- exactly **9 rows**;
- stable IDs `F-*`, `A-*`, `I-*`;
- row thresholds `0 / 5 / 10 / 15 / 20 / 25 / 30 / 35 / 40` points spent in the branch;
- level-60 spend cap **59 points**;
- hybrid builds allowed;
- Fire and Frost have no special cross-school reaction;
- Arcane may improve general Mage parameters but does not require alternating schools;
- no cast pushback mechanic from incoming damage;
- no Mage branch uses a companion/pet mechanic.

The class profile does not grant the three main school attacks automatically. Their first-row talents unlock them:

| Talent | Ability |
| --- | --- |
| `F-1-1` | `MAGE_FIREBALL` |
| `A-1-1` | `MAGE_ARCANE_SPARK` |
| `I-1-1` | `MAGE_ICE_SHARD` |

All offensive spells resolve through the normal Magical damage pipeline. Mage does not create a parallel damage engine.

---

# 2. FIRE / Пламя

## Identity

**Critical hits → Ignite → Scorch → Pyroblast → Combustion.**

Pure Fire is the most aggressive personal magical DPS build: high direct pressure and crit scaling in exchange for noticeable Mana use and less defensive depth.

## Ignite

`F-2-1` causes a direct Fire critical hit to apply Ignite for 4 seconds. Total Ignite damage is the rank-defined percentage of the triggering crit.

A new Ignite never discards unfinished damage from the old Ignite: the remaining amount is rolled into the refreshed effect.

`F-6-3` improves Ignite and allows remaining Ignite damage to transfer from a killed target to another valid enemy according to content.

## Scorch

`F-3-1` unlocks `MAGE_SCORCH`.

`F-3-2` makes Scorch apply Fire Vulnerability for 12 seconds. It stacks to 5; a new hit refreshes the stack duration. The learned rank controls Fire damage taken per stack.

## Pyroblast

`F-4-1` unlocks `MAGE_PYROBLAST` as the heavy Fire cast. Its direct hit can crit and it applies its own short burn. Later Fire talents reduce cast time, improve periodic damage and create instant-Pyroblast windows.

## Hot Streak / Pyromaniac

High Fire rows reward consecutive direct Fire criticals. The streak is tracked server-side and creates the documented instant/discounted Pyroblast window. Non-qualifying results reset or consume the state according to content.

## Combustion

`F-6-1` unlocks `MAGE_COMBUSTION` as the primary Fire burst window.

Combustion:

- replaces the old Heat Limit / Fire Comet loop;
- tracks a limited number of qualifying Fire criticals;
- ends when its crit allowance is consumed or its duration expires;
- receives later damage/crit/Mana upgrades;
- with the capstone, allows four qualifying crits, a longer maximum duration and an instant first Pyroblast after activation.

## Threat / control

`F-1-3` reduces Fire threat and Fireball/Scorch Mana cost by the rank-defined values.

Impact-style Fire stuns can affect normal enemies but never hard-stun bosses.

## Fire active unlocks

| Talent | Ability |
| --- | --- |
| `F-1-1` | `MAGE_FIREBALL` |
| `F-2-3` | `MAGE_FIRE_BLAST` |
| `F-3-1` | `MAGE_SCORCH` |
| `F-4-1` | `MAGE_PYROBLAST` |
| `F-4-3` | `MAGE_FLAMESTRIKE` |
| `F-5-1` | `MAGE_BLAST_WAVE` |
| `F-6-1` | `MAGE_COMBUSTION` |

---

# 3. ARCANE / Тайная магия

## Identity

**Mana → Clearcasting → free casts → Presence of Mind → Arcane Power.**

Arcane is a resource-control and spell-tempo branch. It no longer revolves around Arcane Charges and does not require alternating Fire/Frost/Arcane casts.

## Clearcasting

`A-1-2` gives successful spells the rank-defined chance to grant `MAGE_CLEARCASTING`.

A Clearcasting charge makes the next Mana-cost spell free and is consumed when that spell starts. It must never become permanent merely because the resolved ability cost was already reduced to zero.

Higher Arcane talents can improve the Clearcasting cast, grant regeneration after consumption and increase maximum stored charges to two.

## Presence of Mind

`MAGE_PRESENCE_OF_MIND` creates a short next-cast window. A qualifying cast with base cast time up to 3 seconds becomes instant. Later talents improve its damage and/or Mana cost. The effect is consumed by the qualifying cast.

## Arcane Power

`MAGE_ARCANE_POWER` is the main Arcane burst state. During it, offensive Mage spells receive the documented damage increase and Mana-cost behavior. High-tier talents can provide limited free-cost stacks, extra Clearcasting and Arcane Echo effects.

## Mana Shield

`MAGE_MANA_SHIELD` uses the normal shield/effect pipeline. Absorbed damage consumes Mana; Improved Mana Shield reduces that Mana expenditure. Later talents can refund part of spent Mana after a delay.

## Counterspell

`MAGE_COUNTERSPELL` must interrupt the selected enemy's current interruptible cast via `AbilityEngine.Interrupt`.

Improved Counterspell adds the rank-defined Silence after a successful interrupt. It is not a cosmetic Silence-only effect: a successful Counterspell clears the enemy `ActiveCast`.

## Threat

`A-1-4` reduces threat generated by Arcane spell damage and also provides the documented magic-penetration benefit.

## Arcane active package

The branch unlocks the following production abilities through its talent nodes:

```text
MAGE_ARCANE_SPARK
MAGE_ARCANE_MISSILES
MAGE_ARCANE_EXPLOSION
MAGE_MANA_SHIELD
MAGE_COUNTERSPELL
MAGE_PRESENCE_OF_MIND
MAGE_ARCANE_POWER
MAGE_EVOCATION
```

---

# 4. FROST / Лёд

## Identity

**Chill → Freeze / Deep Chill → Shatter → Ice Lance → Winter's Chill.**

Frost combines damage, control and defensive windows. Boss behavior intentionally differs from ordinary enemies so the branch stays useful without trivializing bosses.

## Chill

Frost abilities apply or refresh Chill where specified. Chill changes combat tempo through the normal effect/stat pipeline. Blizzard and Cone of Cold can strengthen or refresh it according to their talents.

## Freeze versus Deep Chill

Normal enemies may receive `MAGE_FREEZE` as hard control for the documented duration.

Bosses must **never** receive that hard Freeze stun. The same qualifying application instead creates `MAGE_DEEP_CHILL`, a boss-safe debuff consumed/read by Shatter, Ice Lance and later Frost mechanics.

## Shatter

`I-3-1` increases Frost crit chance against Frozen normal enemies. Against bosses under Deep Chill it uses the lower boss-specific value defined in content.

Later talents can improve Shatter and extend a Freeze/Deep Chill state once.

## Ice Lance

`I-6-1` unlocks `MAGE_ICE_LANCE`.

Ice Lance deals its strongest multiplier to Frozen normal enemies and a lower bonus against bosses under Deep Chill. It interacts with Shatter, Perfect Ice Lance and Deep Freeze without applying forbidden boss hard-control.

## Winter's Chill

Frost criticals can apply `MAGE_WINTERS_CHILL`. Stacks increase Frost crit pressure up to the learned rank and expire/refresh according to content. High-tier talents reward reaching the full stack state.

## Ice Block

`MAGE_ICE_BLOCK` lasts 3 seconds and:

- grants damage immunity through the authoritative effect pipeline;
- cleanses supported negative control/debuff effects;
- prevents the Mage from acting for the full duration.

`I-7-3` Cold Blood activates only after Ice Block ends. The next Frost spell inside the talent window receives the rank-defined crit bonus and Mana reduction, then consumes the buff.

## Ice Barrier

`MAGE_ICE_BARRIER` is a normal shield effect. Talents can increase absorb/duration, reduce incoming damage while the barrier exists, Chill the attacker on break, reduce Frost Nova cooldown, restore Mana or create an emergency follow-up shield.

## Threat

`I-3-4` reduces Frost spell cost and Frost threat by the rank-defined values.

## Frost active unlocks

| Talent | Ability |
| --- | --- |
| `I-1-1` | `MAGE_ICE_SHARD` |
| `I-2-3` | `MAGE_FROST_NOVA` |
| `I-3-2` | `MAGE_BLIZZARD` |
| `I-4-1` | `MAGE_COLD_SNAP` |
| `I-4-2` | `MAGE_ICE_BLOCK` |
| `I-4-3` | `MAGE_CONE_OF_COLD` |
| `I-5-2` | `MAGE_ICE_BARRIER` |
| `I-6-1` | `MAGE_ICE_LANCE` |

---

# 5. Channel limitation

The combat kernel currently has no generic `Channelled` ability type.

Until that primitive exists, these intended channels are represented as 4-second `Casted` abilities:

- `MAGE_ARCANE_MISSILES`;
- `MAGE_BLIZZARD`;
- `MAGE_EVOCATION`.

This is an explicit engine limitation, not a change to the intended branch fantasy. Future channel support should replace the representation without changing talent IDs or surrounding mechanics.

---

# 6. Persistence / tree-version migration

Talent IDs are a persistence contract, but rank boundaries may change between published tree versions.

Whenever an existing `CharacterTalentState` is loaded against the current tree:

1. known IDs are retained;
2. a saved rank above current `MaxRank` is clamped to `MaxRank`;
3. zero/negative ranks are removed;
4. IDs absent from the current tree are removed;
5. both normalized loadouts and current `TalentVersion` are persisted;
6. `StateVersion` advances so clients cannot mutate against the stale pre-migration snapshot.

This path exists only for legacy persisted state. New point spending still goes through normal `TalentRules.TryLearn` validation and cannot exceed `MaxRank`.

---

# 7. Required regression coverage

A Mage tree change is not merge-ready unless CI covers at minimum:

- tree shape `32 / 32 / 32`, 96 nodes total, 59-point cap;
- talent/ability content validation;
- legacy persisted-rank normalization;
- rolling Ignite;
- Hot Streak / Fire burst behavior;
- Clearcasting charge consumption;
- Presence of Mind instant/cost behavior;
- Counterspell actually interrupting an enemy cast;
- Freeze on normal targets and Deep Chill on bosses;
- Ice Lance frozen-target multiplier;
- Ice Block action lockout and post-block Cold Blood;
- Browser → Server → PostgreSQL E2E.

`main` must not receive the Mage rework until these checks are green.
