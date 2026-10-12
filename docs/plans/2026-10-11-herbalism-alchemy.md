# Herbalism and alchemy vertical slice

User selected Herbalism + Alchemy after merging living locations (#365). Reuse the profession skill capacity 300, active steps 1–15/16–30/31–60 and current item/craft pipeline. User selected keeping two slots with complete skill loss when explicitly forgetting a profession. Preserve all other progress and inventory; old forget retries must not reset a relearned skill.

1. Content/tests: add two profession definitions, authored herbs and recipes using existing healing/mana consumables; validate source references and progression.
2. Gathering: persistent shared resource nodes with server UTC respawn and stored idempotent receipts. One transaction locks the node, validates location/activity/skill/inventory, grants material and skill, records result and depletion. RNG uses the existing injectable server RNG, never client mutation IDs as seeds.
3. Scene/API: expose resource markers and availability, accept resource identifier + mutation ID as intent, refresh inventory/scene after collection. Reject travel, combat, AFK, wrong location, insufficient skill, unavailable nodes and conflicting retries.
4. Workshop: extend existing UI to select production professions and show available learned/unlearned professions, ingredient counts, consumables and resource routes. Preserve leatherworking/skinning.
5. Verify content, rule tests, PostgreSQL replay/concurrency/inventory rollback/restart tests, Vue tests, builds and mobile browser loop; open a separate PR.

Failure boundaries: inventory-full must not consume a node or advance skill; competing players cannot both claim the same spawn; retry across a respawn or restart cannot duplicate a reward; existing equipment/material locks and craft transactions stay authoritative. No Redis, timed-production subsystem or new combat/consumable engine.

## Completed verification

- Release solution build: zero errors/warnings; 1491 unit and 671 PostgreSQL integration tests passed.
- Frontend: 557 tests passed (Windows used Vitest threads for the full suite); typecheck/build passed; lint has zero errors and existing-style Playwright warnings.
- Strict content/icon/affix/balance/progression audit passed. All six new herb icons use canonical existing assets.
- Four mobile fixture scenarios passed: existing location layout, resource gathering/depletion, leatherworking and alchemy workshop.
- Real ASP.NET/PostgreSQL/production-frontend browser flow passed on a separate temporary database: learn both professions, collect four nodes, craft a healing potion, explicitly forget alchemy, relearn at skill 1.
- Independent review found no blocking issues; partial-stack overflow rollback coverage was added and passed.

Further content beyond skill 61, rare herbs, timed production and receipt-retention policy are outside this slice. Shared nodes currently replenish every 30 seconds; concurrency is authoritative, while balancing population against player count needs live metrics.
