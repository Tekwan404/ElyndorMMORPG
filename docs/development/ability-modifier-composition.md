# Ability modifier composition: authoritative path and extraction

## Production inputs

Content loads immutable base AbilityDefinitions. CombatSessionFactory supplies those
definitions, known ability IDs, derived actor stats, resolved talents and captured
equipment/set identity. TalentModifierResolver aggregates multiple selected generic
modifiers additively into TalentAbilityModifiers; it does not execute abilities.

PvE previously embedded the same nested class graph in UseAbility and ActorSnapshot.
Hosted Arena repeated it in ResolveMechanicsAbilityForSnapshot. ResolvePlayerAbility
also hid generic application followed by Berserker and Paladin transformations;
ResolveMageAbility hid Arcane followed by Frost. Per-target composition had its own
explicit class graph inside CombatSession.

## Current definition path

```text
base AbilityDefinition + current participant's resolved build/runtime
  -> AbilityModifierComposer
     -> TalentAbilityResolver.Apply
     -> Berserker -> Paladin -> Warlord -> Pyromancer
     -> Arcane -> Frost (only Mage + IsSpell) -> Archer
  -> executable AbilityDefinition
  -> existing authoritative target selection/validation
  -> ComposeTarget: Berserker -> Pyromancer -> Mage -> Archer, per enemy
  -> AbilityIntent + AbilityEngine.Execute / CompleteCast
```

AbilityModifierContext carries the current ResolvedTalentModifiers, class identity
and authoritative time. Session-bound stage adapters read the current participant's
runtime/effects rather than copying or caching them. The composer is registered once
per session; builds and executable definitions are not cached. Conditional-effect
synchronization stays before existing command boundaries, not inside composition.
Snapshot composition does not consume effects, spend resources or start cooldowns.

## Field ownership and order

| Property | Definition composition | Other authoritative owner retained |
| --- | --- | --- |
| Cost | Generic percent reductions summed; percent before flat, clamp zero. Berserker conditional percent; Paladin rank discounts/free state; Warlord flat cry discount; Pyromancer sequential percentages; Arcane/Frost conditions; Archer ordered one-shot percentages. | AbilityEngine applies FreeResourceCostWhileEffectId at validation/spend, without rewriting the definition or UI snapshot. |
| Damage/coefficient | Generic damage bonus scales damage Amount/AP/SP coefficients once. Pyromancer, Arcane, Frost, Archer and Paladin multiply existing DamageMultiplier. Arcane overflow scales SP coefficients conditionally. | DamagePipeline uses current actor stats/effects and final per-target modifiers; no damage calculation moved. |
| Healing/coefficient | Generic damage bonus does not scale Healing actions. Existing Arcane coefficient transformations retain their original action semantics; Paladin definition stage reads direct-heal crit flags and adds crit bonuses, not a new healing formula. | HealingPipeline and existing class healing reactions remain unchanged. |
| Cooldown | Generic seconds summed/subtracted/clamped; Paladin, Warlord, Pyromancer, Arcane/Frost ability-specific reductions follow. | Cooldown storage/start/reset/reduction reactions and scheduler stay outside composer. |
| Cast time/type | Pyromancer cast reduction/Hot Streak/Pyromaniac; Arcane Presence of Mind sees the already-reduced cast time; Frost reductions; Archer marked Aimed Shot; Paladin grace/instant conversion. Existing class-specific clamps retained. | AbilityEngine captures ActiveCast; Mage can remain Casted with zero duration, whereas Paladin explicitly converts to Instant. No policy unification. |
| Duration/magnitude | Generic duration seconds/magnitude percentages first; Warlord adds cry bonuses; Paladin Holy Shield/Avenging Wrath duration overrides can replace prior duration bonuses. | Effect application, stacks, refresh, ticking and expiration remain in EffectEngine/class runtime. |
| Charges | No generic charges field exists in executable AbilityDefinition, and composition adds none. RuntimeParameters, EffectDefinition and class state pass through existing stages. | Existing effect stacks/one-shot state and class runtime own consumption; no charge subsystem introduced. |
| Targeting | TargetType/TargetCount/selector/AllowSelfTarget pass through existing stages. Per-target HP/debuff/mark conditions produce a separate modifier for each actor. | CombatSession and Arena select/validate actor IDs; companion/party/hostile policies unchanged. |
| Crit/penetration/accuracy | Additive class crit/accuracy bonuses, crit-damage bonuses and generic armor penetration; target-specific freeze/mark/debuff/HP conditions applied in the original order. | Damage/Healing pipelines resolve hit and crit with existing RNG. |
| Generation/refund | ResourceChange actions and runtime parameters retain their values; composition itself does not generate/refund resource. | AbilityEngine executes actions; lifecycle/event class hooks handle refunds, including pending Mage refunds and Paladin Illumination. |
| Class properties | Berserk silence/stun exceptions, Paladin cast-type changes, all other metadata/flags preserved. | Availability, weapon/mobility/CC validation remain where they were. |

EquipmentStatModifierResolver applies active set stat bonuses before combat actor
assembly. SetPassiveRuntime evaluates authoritative events and executes existing
actions; it is not a separate AbilityDefinition composition stage. Likewise actor
stat/effect damage/healing multipliers are not folded into coefficients a second
time by this component.

## PvE and Arena

Hosted Arena uses CreatePlayerMechanics and the same composer from unmodified
BaseAbilities. ArenaFighterAssembler also prepares a statically modified fallback
dictionary; hosted execution intentionally does not feed that dictionary through
generic modifiers again. Standalone synthetic fighters retain the existing
ArenaTalentRuntimeSupport/static resolvers and fallback runtime. No fallback was
removed or reordered.

Existing cast-target timing is intentionally different: PvE stores per-target
modifiers on cast start; Arena computes them at cast completion against current
target state. Composition returns identical modifiers for identical contexts, but
this extraction does not change those capture boundaries or PvP CC/DR.

## Characterization and verification

25 real-session characterization cases passed on the untouched production code
before extraction. Definitions captured from ActiveCast/PendingAbilityAction after
real UseAbility are compared in full between PvE and hosted Arena, with literal
numeric expectations independent of the implementation. Cases include additive
generic talents, class multipliers/flat discounts, cooldown/cast time, conditional
resource state, marks/one-shot buffs, crit, duration overrides, unchanged healing/
resource actions, targeting and metadata, and snapshot purity.

Four further regression cases cover active-participant changes in a shared party
session, execution-time free-cost flags in both PvE/Arena, and independent modifiers
for multiple enemies. The pre-existing cast-target capture distinction is explicit
in the characterization suite.

A deliberate local mutation moved Berserker percentage after Warlord flat cost.
The characterization failed with expected 45 versus actual 46.4; original ordering
was restored before subsequent regression runs. No mutation is part of the diff.

Final local verification: 29 composition characterization cases and 786 focused
combat/PvP/talent cases pass. The full Release solution suite passes 1051 unit and
485 integration tests, with zero failures/skips (PostgreSQL/Testcontainers). Release
solution build passes with zero warnings/errors; git diff --check is clean.
Frontend checks were not rerun because no frontend files/contracts changed.

## Extraction boundary

CombatEventRouter is unchanged. The composer owns composition policy/order and
generic application, not execution or post-event reactions. Existing class-stage
formulas and access to private runtime state remain in class partials behind bound
adapters; extracting each class implementation is a separate responsibility pass.
No formulas, balance/content, actor mutations, scheduler, death, threat, target
selection, rewards, persistence, API or frontend contracts were changed.
