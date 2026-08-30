---
name: elyndor-review
description: Perform the final Elyndor code and architecture review before completing a feature or phase block. Use to inspect diffs for correctness, safety, test gaps, documentation drift, and scope creep.
---

# Elyndor Review

Review the complete diff against `AGENTS.md`, the current phase, and the relevant Source of Truth. Read code in context; do not review only isolated hunks.

## Review areas

- Correctness and edge cases
- Security and server authority
- Race conditions, idempotency, transaction boundaries, and duplicate rewards
- Persistence, reconnect, restart, and partial-failure behavior
- Dependency direction and module ownership
- Duplication, dead code, unnecessary abstraction, and giant services/controllers
- Missing or superficial tests
- Naming and contract consistency
- Source of Truth drift and future-phase scope creep
- Accidental secrets, unsafe development auth, and raw production exceptions

## Findings format

List actionable findings first by severity:

- Critical: data loss, exploit, secret exposure, broken economy, or unusable release.
- High: likely incorrect feature, race, security weakness, or architectural violation.
- Medium: maintainability, recovery, UX, or testing gap with meaningful impact.
- Low: small consistency or cleanup issue.

Include file and line references plus a concrete fix. If no findings remain, say so and state any verification limitations separately.
