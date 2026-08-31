# Phase 3A Combat Kernel Implementation Plan

## Goal

Deliver a deterministic, server-authoritative, headless combat kernel without Monster System, production class kits, talents, or combat persistence.

## Block A — Effects

- Add typed effect definitions, active instances, stack policies, control states, shields, and deterministic periodic processing.
- Cover refresh, replace, stack, independent, strongest-wins, expiration, dispel, DoT, HoT, and exact timestamp boundaries.
- Keep collection mutation staged so effects applied or removed during a processing pass cannot corrupt iteration.

## Block B — Damage and healing

- Add injectable deterministic RNG.
- Implement ordered hit/dodge/critical/penetration/mitigation/modifier/minimum/shield/HP resolution.
- Return structured damage and healing results and events.

## Block C — Abilities

- Add data-driven definitions and runtime state for resource, cooldown, GCD, cast, interrupt, lockout, and queue boundaries.
- Accept only intent and derive all authoritative outcomes in Core.
- Add idempotent command handling to the headless runtime boundary.

## Block D — Content and UI

- Extend the existing package with optional effect and ability collections while preserving `content/package.json` compatibility.
- Validate identifiers, duplicate IDs, references, values, and supported mechanics.
- Add Arcane Minimal ability, cast, and active-effect primitives to the existing development playground.

## Verification and delivery

- Run focused tests after each domain block, then complete backend and frontend verification.
- Review diff, scope, secrets, and generated assets.
- Commit and push the completed pass to `main`.
