---
name: elyndor-combat
description: >
  Elyndor combat architecture, implementation, debugging, and regression skill.
  Use automatically whenever a task touches CombatSession, combat actors,
  attacks, abilities, talents, cooldowns, targeting, threat, buffs, debuffs,
  healing, damage, companions, enemies, multi-enemy combat, party combat,
  dungeon combat, combat reconnect, combat persistence, combat finalization,
  loot eligibility, XP, or rewards.
  Raid development is currently frozen and must not influence active combat
  architecture unless the user explicitly reactivates raid work.
---

# Elyndor Combat

## Purpose

Protect the correctness and stability of Elyndor's combat runtime.

Combat changes have a large regression surface.

Never modify targeting, actor identity, lifecycle, rewards, or captured participant state without tracing the complete execution path.

Combine this skill with `elyndor-development`.

---

# Current development scope

Active combat priorities are:

- solo combat;
- companions where supported;
- Party combat;
- dungeon combat;
- multi-enemy combat;
- abilities;
- talents;
- threat;
- targeting;
- reconnect;
- rewards;
- combat stability.

## Raids are frozen

Raid functionality is currently deferred.

Do not redesign Party, CombatSession, targeting, rewards, companions,
persistence, or other existing systems around hypothetical future raid requirements.

Do not add:

- raid-specific targeting abstractions;
- raid subgroup logic;
- hidden 5-player subdivisions;
- raid-specific persistence;
- raid-specific reward rules;
- raid-specific participant models;
- speculative raid extensibility layers;

unless the current user task explicitly reactivates raid development.

Preserve unfinished raid-related code where safe, but do not expand it during unrelated work.

Do not make active systems more complex merely to support a future raid design.

When raids are revisited, their architecture must be designed from the actual requirements at that time.

---

# Fundamental identity rule

Do not treat:

- account;
- character;
- combat actor;
- companion;
- Party member

as interchangeable identities.

Always determine which identity the current operation actually requires.

---

# Combat identity

When working with captured combat participants inspect the relevant identity fields, including where applicable:

- AccountId;
- CharacterId;
- ActorId;
- companion ownership;
- Party identity.

Never use ActorId where persistent CharacterId is required.

Never use mutable external Party membership when combat correctness requires captured immutable membership.

---

# Captured combat state

Once a combat session starts, membership used by combat mechanics should generally be derived from captured combat state rather than mutable outside state.

This matters for:

- targeting;
- buffs;
- rewards;
- reconnect;
- disconnect;
- Party mutations;
- group membership changes during combat.

Do not silently re-resolve critical combat membership from the live Party after the session has captured participants.

---

# Party rules

Party is the active cooperative combat-group model.

Do not redefine Party semantics to prepare for raids.

Party-targeted mechanics should operate against actual Party membership and the captured combat participants that belong to that Party.

Do not introduce artificial group layers inside Party.

---

# Targeting

Every targeting implementation must explicitly identify its semantic scope.

Examples:

- Self
- CurrentTarget
- Ally
- SingleAlly
- PartyMember
- PartyMembers
- SelfAndPartyMembers
- Companion
- SelfAndCompanion
- Enemy
- Enemies
- NearbyEnemies
- AllEnemies
- AreaAllies
- AreaEnemies
- DeadAlly

Do not overload one selector to mean different scopes depending on accidental runtime state.

Do not add raid-aware selectors while raids are frozen.

---

# Targeting review checklist

For every targeting change test or reason through:

## Solo

- player only;
- no companion;
- companion present where supported.

## Party

- caster;
- one ally;
- multiple allies;
- full Party;
- companions where applicable;
- dead/inactive members if applicable.

## Enemy state

- one enemy;
- multiple enemies;
- current target dead;
- secondary target dead;
- all targets dead.

---

# Companion mechanics

Companions must not be accidentally lost when changing participant enumeration or targeting.

Whenever changing participant enumeration answer:

- Is the companion represented as a separate ActorId?
- Who owns it?
- Should this effect apply to the companion?
- Does Party-targeting include companions for this specific effect?
- Can companions receive rewards?
- Is companion state restored on reconnect?

Preserve existing companion semantics unless the task explicitly changes them.

Do not generalize companions around hypothetical raid behavior.

---

# Multi-enemy combat

Never assume one enemy per combat session.

Any new combat logic should be reviewed for compatibility with:

- multiple active enemies;
- explicit current target;
- AoE;
- enemy death during a tick;
- target replacement;
- auto attack;
- threat;
- per-enemy state;
- rewards.

Avoid global single-enemy fields when state belongs to a specific enemy.

---

# Damage and healing

Keep the calculation pipeline deterministic and explainable.

Prefer:

base value
→ attacker modifiers
→ ability/talent modifiers
→ target modifiers
→ mitigation
→ critical/block/parry/etc.
→ final result

Do not scatter unrelated hardcoded coefficients across class-specific partials if they belong in content/configuration.

When adding a modifier verify:

- stacking;
- ordering;
- caps;
- rounding;
- critical interaction;
- armor/resistance interaction;
- PvE/PvP applicability if relevant.

---

# Abilities

For ability changes inspect:

- availability;
- resource cost;
- cooldown;
- target validation;
- range/state restrictions if applicable;
- execution;
- damage/healing;
- secondary effects;
- talents;
- presentation data;
- persistence/reconnect of cooldown state.

Server must validate the ability independently of the client.

---

# Talents

A talent should not appear implemented merely because it exists in content/UI.

Verify the runtime effect is actually wired.

For a talent change identify:

1. definition;
2. rank values;
3. stat/derived-state contribution;
4. runtime hook;
5. combat event;
6. client presentation;
7. tests.

Avoid talent descriptions whose effect is not implemented.

---

# Threat

Threat calculations must have a clear source actor and target enemy.

Review:

- damage threat;
- healing threat;
- explicit threat modifiers;
- taunt;
- target switching;
- dead actors;
- multi-enemy behavior.

Never assume a threat operation applies to only one enemy when multiple enemies may exist.

---

# Auto attack

When modifying auto attack inspect:

- start/stop;
- target validation;
- tick cadence;
- dead target handling;
- reconnect;
- rate limiting;
- concurrent ability execution;
- class-specific modifiers;
- multi-enemy interactions.

Do not permit duplicate loops or duplicated attacks after reconnect/restart.

---

# Combat concurrency

Combat runtime may receive concurrent events.

Inspect:

- commands arriving during ticks;
- ability use while auto attacking;
- reconnect;
- disconnect;
- cancellation;
- finalization;
- actor death;
- enemy death;
- repeated client commands;
- duplicate completion.

Critical transitions should be idempotent or guarded.

Never rely solely on the client not sending duplicate requests.

---

# Combat finalization

Finalization is a high-risk area.

It must not:

- run twice;
- issue rewards twice;
- leave sessions permanently active;
- lose rewards after a successful kill;
- persist partially completed combat state without recovery.

Review all relevant completion paths:

- victory;
- defeat;
- flee;
- dungeon completion;
- disconnect;
- cancellation;
- timeout where applicable.

---

# Rewards

Reward eligibility must use the correct captured participant model.

Check:

- XP;
- currency;
- normal loot;
- boss loot;
- dungeon rewards;
- contract progress.

Never derive reward recipients purely from mutable live Party state after combat if participants were captured at start.

Prevent:

- duplicate rewards;
- rewards to non-participants;
- missing rewards to valid captured participants;
- duplicate completion after reconnect.

---

# Kill credit

Kill credit must be based on the intended combat-participation model.

When changing kill or reward logic verify:

- which CharacterIds were valid participants;
- whether disconnected participants remain eligible;
- whether dead participants remain eligible;
- whether companions affect ownership or attribution;
- whether duplicate finalization can issue credit twice.

Do not infer kill credit from the client.

---

# Inventory interaction

Combat loot must respect the canonical inventory-capacity system.

Do not introduce a separate combat-specific inventory limit.

When inventory is full:

- follow the project's defined fallback behavior;
- do not silently delete valuable rewards.

Stacking should be attempted according to item stack rules before declaring lack of capacity.

---

# Reconnect

Reconnect must restore or reattach to the same logical combat session when appropriate.

Check:

- actor identity;
- CharacterId;
- current HP/resource;
- cooldowns;
- targets;
- combat status;
- captured Party state;
- rewards/finalization state.

Reconnect must not:

- create a second session;
- duplicate rewards;
- restart completed combat;
- duplicate auto-attack loops.

---

# Runtime errors

Do not swallow combat exceptions silently.

At minimum log context such as:

- session ID;
- actor ID;
- CharacterId where relevant;
- operation;
- ability;
- target;
- phase/tick.

Cancellation should be distinguished from unexpected failure.

---

# Combat testing

For a focused combat change start with the closest tests.

Then expand to relevant runtime regression coverage.

When applicable verify:

- solo combat;
- companion combat;
- Party combat;
- dungeon combat;
- multi-enemy combat;
- reconnect;
- rewards;
- finalization.

Do not run or expand raid-specific regression coverage unless the user explicitly resumes raid development.

---

# Mandatory regression matrix

For changes to central combat systems consider the relevant subset of:

## Solo

- one player;
- one enemy;
- multiple enemies;
- target dies;
- player dies.

## Companion

- player without companion;
- player with companion;
- companion ownership;
- companion targeting;
- reconnect with companion.

## Party

- 2 members;
- full Party;
- single-target ally ability;
- Party-wide effect;
- one member disconnects;
- one member dies;
- live Party changes after combat starts where supported.

## Multi-enemy

- current target;
- secondary enemies;
- AoE;
- threat per enemy;
- enemy death during tick;
- reward/finalization after last enemy dies.

## Lifecycle

- disconnect;
- reconnect;
- duplicate command;
- repeated finalization attempt;
- CharacterId remains correct;
- kill credit;
- reward eligibility;
- no duplicate rewards.

---

# Completion criteria

Before describing combat work as complete:

- focused tests pass;
- combat runtime builds;
- Party semantics were checked when relevant;
- companion behavior was checked when relevant;
- multi-enemy behavior was checked when relevant;
- reward implications were checked;
- reconnect/finalization implications were checked;
- no new silent failure path was introduced;
- no speculative raid abstraction was added.

When changing a central resolver, participant registry, or finalizer, assume the regression radius is large until demonstrated otherwise.
