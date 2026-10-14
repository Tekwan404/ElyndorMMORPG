# Dungeon and profile polish

Remove the synthetic dungeon-floor navigation and grouping. Preserve ordered encounters,
existing runs and rewards; keep the optional legacy floors response field empty for older
clients. Show the complete dungeon loot list without floor selection.

Allow ASCII digits 0-9 in character creation and paid rename using the existing shared
server policy. Keep length, normalization, single-script letters, separator and uniqueness
rules. Update client validation/help; test digits, mixed scripts and non-ASCII numerals.

Replace stretching SVG profile ornaments with thin borders and fixed-size decorative
corners, preserving entitlement selection and the three styles. Keep the portrait and
full-body hero unobscured. Make the HUD crystal counter one compact line, keep the exact
balance and accessible store entry. Existing components retain their responsibilities:
ProfileFrame owns ornament styling; CrystalBalanceButton owns the shared wallet shortcut;
DungeonLocationCard owns ordered-run display.

Verify focused backend/name and PostgreSQL rename tests, frontend regressions, build,
lint/typecheck, content validation, and mobile/browser visual checks at 320/390/580 widths.
No schema changes, new images/dependencies, payment work or combat-rule changes.

## Verification results

Release solution build: zero warnings/errors. Name-policy unit tests: 24 passed.
PostgreSQL creation/rename/dungeon checks: 24 passed. Focused Vue checks: 35 passed,
plus 8 after the final digit-input test update. Frontend build/typecheck passed;
lint has zero errors and the existing 22 warnings. Content package validates.
The dungeon mobile E2E passed. Browser checks at 320/390/580 px preserved the exact
nine-digit crystal balance, measured a 28px wallet and 10px frame corners, and
confirmed rename with digits. API mocks support visual checks; PostgreSQL tests
separately verify authoritative name persistence and paid rename behavior.
