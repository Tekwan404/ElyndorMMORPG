# Combat resource runtime implementation plan

> Execute inline with executing-plans and test-driven-development; preserve the characterized production behavior.

**Goal:** Extract resource orchestration without changing gameplay.
**Architecture:** Actor state remains authoritative. CombatResourceRuntime owns mutation-result creation and elapsed regen integration. Session adapters supply existing class rules, participant activation and publication; engine events are never replayed as resource commands.
**Tech Stack:** .NET 10, C#, xUnit.
**Spec:** User-approved resource extraction; `docs/source-of-truth/gameplay/07_RESOURCE_SYSTEM.md`.

## Constraints

No balance/content, scheduler, targeting, damage/healing, event-router or proc-guard changes. Preserve publication-only grants versus routed engine results, zero-event policy and identity/proc metadata. No persistence/transaction changes: execution remains under the existing session single writer.

## Execution

- [x] Characterize spend/generation/refund and regen boundaries in `tests/Elyndor.UnitTests/Combat/CombatResourceCharacterizationTests.cs` on unmodified production. Assert literal resources, event deltas/order and PvE/hosted Arena parity; reuse existing block/proc/party/companion suites.
- [x] Add isolated runtime tests for successful/failed spend, clamp, zero publication, class scaling and piecewise regen. Observe missing-runtime compilation failure before extraction.
- [x] Add `Combat/Resources/CombatResourceRuntime.cs`: `Change` returns the actual authoritative delta/event; `TrySpend` returns an event only after successful spend; instance grants publish through the existing sink. Regen consumes explicit boundaries and existing rate adapters.
- [x] Bind `CombatSession.Resources.cs` to existing class scaling/regen rules; reduce session orchestration to participant/time coordination. Wire AbilityEngine resource operations (not execution) and Arena fallback regen/grants to the same primitive, preserving fallback silent autoattack gains.
- [x] Run focused characterization, full combat/PvP/talent tests, full solution/integration tests; inspect diff and ownership map in `docs/development/combat-resource-runtime.md`.
- [x] Commit, push and open a new PR. Do not merge main in this task.
