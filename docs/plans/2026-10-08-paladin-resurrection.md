# Paladin resurrection

Approved scope: a base Paladin ability targeting a dead captured ally during an
active combat. No group quota and no PvP prohibition. Solo Arena has no eligible
ally; this change does not add group Arena.

Initial content balance: 6 second interruptible Holy cast, 5 minute personal
cooldown, 80 Mana, restore 30% maximum HP and 20% maximum resource. Values live
in content and can be tuned independently. No resurrection immunity is added.

1. Test an explicit dead-ally target and resurrection action in the ability kernel.
2. Restore the existing participant from Dead to Active; retain the same runtime,
   cooldowns, consumed items and contribution. Cancel obsolete casts on death.
3. Add the base ability and RU presentation; allow selecting dead party members
   in the existing battle UI while keeping normal healing restricted to the living.
4. Test interruption, invalid targets, competing casts, repeated death and
   resurrection, preserved runtime state, and solo Arena compatibility.
5. Run backend/frontend checks, content validation and review; PR and merge only
   after required CI passes.

The combat session remains the single writer. Eligibility is checked against its
captured roster at cast start and the corpse is checked again at completion.
Resurrection is not healing and cannot trigger healing procs or duplicate rewards.
An ended combat cannot be reopened. No schema migration or new reward operation
is needed; authoritative snapshots must expose the restored participant state.

After the user's main update (80a149b5), finish the five explicitly remaining
Paladin P1 contracts in the same functional pass: Wisdom/Light marks with per
beneficiary ICD, Consecration threat, Fanaticism threat limited to Retribution
damage, and one-hand + shield specialization based on captured equipment.
Author missing magnitudes/ICD/chance in talent content. Preserve all existing
P0 admission fixes and the 96 talent IDs; no global class rebalance.
