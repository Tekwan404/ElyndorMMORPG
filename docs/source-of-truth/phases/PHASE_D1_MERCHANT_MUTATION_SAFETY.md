# Phase D1 — Merchant Mutation Safety

Status: implemented, automated verification pending

Phase D1 closes the first gameplay-integrity debt before Content Platform work.

Merchant write requests now carry a UUID `MutationId`. Successful mutations are recorded in
`game.character_mutations` under the composite primary key `(CharacterId, MutationId)`, together
with operation type, SHA-256 request fingerprint, and UTC commit timestamp.

Same id + same payload replays success without applying side effects again.
Same id + different payload returns `merchant_mutation_conflict`.
Failed operations roll back the reservation.

Gold changes are database deltas rather than stale read/modify/write values:

```text
buy:    Gold = Gold - price WHERE Gold >= price
credit: Gold = Gold + amount
```

Combat reward Gold uses the same atomic-delta rule so a concurrent reward cannot overwrite a
merchant balance update.

Integration coverage was added for replay, mutation-id conflicts, concurrent overspend,
stack preservation, and concurrent selling of the same material unit.

Do not report this phase as green until CI or a local test run actually executes it.


## Inventory mutation safety

The same character mutation ledger now protects normal inventory writes:

- equip;
- unequip;
- out-of-combat consumable use.

Each request carries a MutationId. Successful replay is side-effect free; conflicting reuse returns
`inventory_mutation_conflict`.

All inventory writes acquire a PostgreSQL `FOR UPDATE` lock on the character row before reading
mutable inventory/vitals state. Merchant writes use the same lock order. Combat consumable
consumption also takes that lock, so REST inventory actions, merchant mutations and combat
consumption cannot race on one character's item stacks.

Integration coverage includes duplicate consumable replay, cross-operation mutation conflicts,
two concurrent uses from one stack, two requests racing for the final consumable, and concurrent
writes to one equipment slot.


## Equipment class restrictions

Equipment definitions now carry optional class restrictions plus typed category ids:

```text
WeaponCategory
ArmorCategory
AllowedClassIds
```

Server equip validation applies, in order:

```text
ownership
→ equipment type / slot
→ required level
→ explicit AllowedClassIds
→ class AllowedWeaponCategories
→ class AllowedArmorCategories
→ equip
```

Weapon and armor category vocabularies are game rules in C#; concrete item category assignments live
in content. Current Ranger weapons are `ONE_HAND_SWORD` and Ranger armor pieces are `MEDIUM`.
Accessories remain category-neutral unless an explicit class restriction is added.

Content validation rejects unknown categories, invalid slot/category shapes, duplicate class
restrictions and references to classes that do not exist.
