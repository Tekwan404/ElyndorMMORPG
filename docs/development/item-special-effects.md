# Item special effect runtime

Equipment can reference `ItemDefinition.SpecialEffectIds`. Definitions live in
`GameContentPackage.ItemSpecialEffects`; category fragments compose them by ID.
Content validation rejects missing or duplicate references, invalid actions and
cooldown actions that reference missing abilities.

The combat factory captures equipped IDs and definitions with each participant.
Solo, party and hosted Arena mechanics use the same event router and runtime.
Loadouts remain fixed for the session. Counters and cooldowns belong to an actor
and effect ID; wearing two items with the same ID does not double the proc.

Triggers filter combat kernel event type, source/target owner, definition IDs and damage
type. Conditions support `EveryNth` and `InternalCooldown`. Events during ICD do
not advance the counter. Replayed events, periodic damage, reflected damage and
secondary procs cannot trigger another item proc.
Use `AbilityCompleted` for completed ability interactions; `AbilityUsed` is a
session log entry, not a dispatched kernel event.

Actions support timed stat effects, shields, direct secondary damage, healing,
resource restoration and cooldown reduction. Timed effects are source specific.
Damage and healing use the combat kernel; resource changes clamp to capacity.
`ScaleWithMaxHp` uses a fraction (0.05 means five percent), while
`EventAmountPercent` uses percentage points (5 means five percent).

The bundled L60 catalog assigns one authored combat mechanic to every branch-specific
Unique weapon, ring and cloak (36 total). These effects use the shared runtime only:
no item ID is hardcoded in CombatSession. Weapons primarily create offensive branch
moments, rings reinforce resource/proc loops, and cloaks provide reactive defense or
counter-pressure. The catalog lives in `content/items/l60-branch-uniques.json`
and is regression-tested so every L60 Unique resolves exactly one distinct effect.

Current authoring limits still apply: item effects cannot inspect arbitrary health
thresholds, consume custom charges, test target/owner status effects, coordinate a
pet-owner alternating sequence, create resource overflow above capacity, or prevent
a lethal hit. Those mechanics require extending the shared item runtime rather than
adding item-specific CombatSession branches.
