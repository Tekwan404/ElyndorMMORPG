# Spatial Inventory V1

Elyndor uses one unified inventory. Base capacity is 30 slots. One equipped spatial artifact adds a non-combat capacity bonus. Spatial artifacts are stored in a dedicated character state and do not occupy combat equipment slots.

A spatial artifact can only be unequipped or replaced when the projected inventory still fits the projected capacity after the operation. The returned artifact itself occupies one normal inventory slot. Player actions therefore cannot deliberately create an overflow state.

Earned rewards continue to use the common inventory capacity and pending-loot paths. Existing stacks may still be filled when no free slot exists; merchant purchases that require a new slot remain rejected when there is no capacity.
