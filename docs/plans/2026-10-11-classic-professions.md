# Classic profession workshops

Scope: retain Skinning/Leatherworking and add a small Mining -> Smelting -> Blacksmithing loop plus Disenchanting -> Scroll craft -> Enchantment. Preserve two primary professions, current equipment budgets and skill ceilings. Present all five professions with generated art in the existing Vue workshop.

Server transactions must reject wrong location, active travel, insufficient skill, missing/protected ingredients and full inventory. Idempotency fingerprints prevent replayed rewards or consumption. Shared mining cooldown must survive profession forgetting. Enchantments replace a single persisted bonus and preserve generated metadata.

Implementation: extend composed content and validation; add profession actions and additive EF migration; apply enchants through the existing inventory/equipment projection; generalize workshop selection and add confirmation for destructive actions; verify PostgreSQL concurrency/replay, unit rules, frontend typecheck/build and mobile browser layouts.

City integration: add an illustrated craft courtyard and real station navigation, targeted learning, return-to-district behavior, and retain equipment enhancement/reforging. Verify all four station routes and actual crafting at 320px in the browser.
