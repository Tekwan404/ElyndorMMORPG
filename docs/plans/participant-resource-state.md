# Participant-owned temporal resource state

Base: main 11660941, after #284. Work on fix/participant-resource-state only.

1. Characterize existing single-player PvE/hosted Arena timing and inventory all
   session-local resource clocks, queues and counters.
2. Reproduce cross-participant Meditation, Mana Shield refund, Arcane Power spend
   counter, Combustion's Mana recovery window and Archer Combat Rhythm interference
   on unmodified production.
   Also characterize Hot Streak crit history and Cold Blood readiness because
   both directly control participant-specific Mana discounts.
   Review also identified Preparation's Focus recovery flag and owner starvation
   at the existing PvE enemy-action/cast-completion Mage sync boundaries.
3. Move only those nine values to existing CombatPlayerRuntimeState; keep active
   participant adapters, all call sites, formulas and scheduler boundaries intact.
   At the same two existing PvE Mage sync boundaries, sync each active living Mage
   and restore the prior context. Add no new boundary or scheduler wakeup.
4. Verify multiple refunds, active switching, death/flee/reconnect, hosted Arena,
   full Release solution build and unit/integration suites; review diff and open PR.

In-memory participant state is guarded by the existing session single writer.
There is no persistence/schema/transaction change. Reattach retains the participant
state; terminal roster entries cannot reattach. No new ProcGuard or runtime exists.
