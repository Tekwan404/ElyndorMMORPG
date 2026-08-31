# Phase 3B — Warrior Ability Kit

Status: approved for implementation on 2026-08-31.

## Goal

Connect the first production class kit to the shared Combat Kernel without introducing CombatSession, monsters, talents, progression, or rewards.

## Prototype kit

| Ability | Rule |
| --- | --- |
| `STRIKE` | Instant, 0 Rage, 80% Attack Power physical damage, generates 10 Rage, standard GCD. |
| `SHIELD_BASH` | Instant, 20 Rage, 70% Attack Power physical damage, Stun for 1.5 seconds, 8 second cooldown, standard GCD. |
| `PROVOKE` | Taunt, 15 Rage, ForcedTarget intent for 3 seconds, 8 second cooldown, standard GCD. Threat runtime remains owned by Phase 4. |
| `HEAVY_BLOW` | Instant, 30 Rage, 160% Attack Power physical damage, 4 second cooldown, standard GCD. |
| `BATTLE_FOCUS` | Instant, 20 Rage, increases Attack Power by 15% for 6 seconds, 20 second cooldown, off-GCD. |
| `BATTLE_SHOUT` | Instant, 0 Rage, generates 20 Rage, 15 second cooldown, off-GCD. |

All values are versioned content. Rage is clamped to the authoritative maximum. Damage, effects, cooldowns, GCD, command idempotency, and resource changes execute server-side through Phase 3A.

## Talent boundary

`BASTION`, `WILD_STRIKE`, and `WHIRLWIND` remain talent-only abilities owned by Phase 3C. Their existing visual assets may be shown as locked previews but must not be executable or included in the Phase 3B known kit.

## Verification

- one deterministic headless sequence proves Attack Power scaling and Rage generation/spending;
- content validation proves the six IDs are unique and supported;
- no Monster, Threat runtime, Talent runtime, XP, loot, or persistence migration is introduced.
