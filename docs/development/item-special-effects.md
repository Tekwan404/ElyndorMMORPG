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

This is execution infrastructure. The current bundled catalog does not yet assign
36 branch-specific Unique weapon/ring/cloak mechanics. Those require authored
content and balance verification; the infrastructure alone does not change them.
