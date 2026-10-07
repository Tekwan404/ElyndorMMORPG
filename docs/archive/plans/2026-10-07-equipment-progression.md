# Equipment progression and set effects

Implemented on `feat/equipment-progression-set-effects`. Operational contract:
[`equipment-set-progression.md`](../../development/equipment-set-progression.md).

Approved scope: reorganize the sixteen existing leveling families into L18/L35/L45/L55 tiers, preserve early template IDs and art, keep Normal60/PvP60 stat-only, and give PvE T1 rotation/signature effects.

## Decisions

- L18 uses Head/Chest/Hands/Legs. L35/L45/L55 use six armor slots. L60 uses six armor slots plus Amulet/Ring1; bonuses stop at six pieces.
- Preserve old templates and level requirements. L35/45/55 variants reuse art with new template IDs; existing set identities follow their designated tier.
- Derive flat bonuses and structural scaling from the existing itemization curve/weights. Percentage bonuses remain authored design values.
- Extend SetPassiveRuntime; effects are captured from content with the participant. No set-ID dispatch in CombatSession.
- Counters belong to actor/effect, admit only authoritative direct events, exclude proc/reflection/periodic loops, and honor ICD before accumulating progress.
- Timed/charged bonuses expire; cooldown reduction cannot move readiness before now. Resource refunds clamp to maximum.
- No persistent schema change or reward transaction change is intended. Published content must follow the existing publication process.

## Work

1. Add failing catalog contracts for tier density, old-template preservation, 4/6/8 slots, source reachability and Holy naming.
2. Reorganize definitions, add scaled variants and appropriate level loot sources; validate the composed package.
3. Add failing runtime tests for filters, resource windows, EveryNth/ICD, actor isolation, charged damage/healing, cooldown reduction and companion ownership.
4. Extend the existing passive model/evaluator/executor and capture content passives in all combat entry paths/build snapshots.
5. Author L55 and twelve PvE T1 packages. Replace old 4/6 stats rather than stacking mechanics on top.
6. Update tooltip contracts/presentation where necessary and Source of Truth. Run backend build, focused and broader tests, validator, frontend typecheck/build and affected frontend tests.
7. Inspect final diff, report actual verification and remaining limitations.

## Balance choices requiring explicit documentation

Resolve underspecified durations/windows from existing ability cadence, document the chosen values in content descriptions, and test their boundaries. Mechanic power cannot be proven by static budget alone; compare representative full-set and mixed loadouts with deterministic runtime tests/benchmarks.

## Verification

- Debug and Release solution builds: zero warnings/errors.
- Backend unit suite: 1318 passed.
- Full PostgreSQL integration suite: 609 passed; subsequent focused set suite:
  17 passed, including the two added standalone-roll comparisons.
- Content validator accepts 0.42.0 / 0.32.0, 878 items. Existing optional missing-art
  warnings remain; frontend coverage confirms all new set members resolve artwork.
- Frontend: 503 tests passed, typecheck/build passed, lint has zero errors and
  thirteen existing E2E warnings.
- Playwright shell smoke passed on the dedicated local Vite server; the initial
  cold-server attempt timed out at character creation. A direct 320px component
  check confirmed 6/8, all three active thresholds and no horizontal overflow.
- Flat bonus generator is idempotent. All 164 old progression templates retain
  their identifiers, names, artwork and required levels.
- Branch performance across every encounter is not established by these static
  budget and trigger tests; encounter-specific DPS/HPS tuning remains ongoing
  balance work rather than a claim of universal branch parity.
