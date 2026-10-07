# Warrior mechanical contracts

Base: origin/main 0fb5bf7a. Numerical balance is out of scope until mechanics pass.

1. Restore STRIKE at level 1, BATTLE_SHOUT at level 3 and HEAVY_BLOW at level 6; test composed content, known abilities and execution.
2. Repair ENDURANCE_CRY maximum health and RALLY_CRY target-relative healing through shared effect/healing pipelines. Cover expiration, dispel, retry and multiple targets.
3. Audit the composed 20 talent unlocks through CombatSession and hosted Arena mechanics. Cover actual damage, control, resources, effects and events; investigate mismatches with narrow regressions.
4. Repair Warlord support participant enumeration, event routing, durations and vengeance/banner triggers. Preserve captured membership and proc policy.
5. Migrate duplicated hooks only with behavioral parity tests; normalize Warrior content and Russian presentation after runtime stabilizes. Review active buttons and T1 interactions.
6. Run wider combat/talent tests, content validator, backend build and client checks; inspect final diff.

Failure boundaries: commands remain idempotent; invalid targets must not spend Rage; dead/inactive allies must not receive support; temporary health must unwind on every removal path. Runtime changes remain within the existing session single writer. No database or reward transaction changes are planned.
