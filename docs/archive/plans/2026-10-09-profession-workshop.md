# Existing profession workshop completion

Goal: finish the existing skinning → processing → useful gear loop in the city before adding professions.

Keep the current skill scale 1–300 and existing server-authoritative, serializable, replay-safe mutations. Do not invent high-level gathering zones to fill the scale. Available progression is explicitly described through recipe skill-up limits and material sources.

- [x] Add regression tests for recipes consuming light/thick leather, actionable recipe states, and protected inventory ingredients.
- [x] Complete the existing material tiers with ordinary crafted gear, using existing art and item generation budgets. No Rare+ mass production.
- [x] Enrich read-only profession state with canonical item names, growth limits and material sources from actual location encounters. No persistence migration required.
- [x] Make the city workshop show processing/gear recipes, locked requirements, skill growth and where ingredients are acquired; exclude locked/equipped/transaction-locked ingredients from availability.
- [x] Verify content, backend build/tests, frontend unit/typecheck/build/lint and mobile browser crafting flow; review and open PR.

Failure cases: insufficient skill/materials, wrong location, full inventory, network failure/replay, locked ingredients and skill-up expiry. Economic writes remain in the existing ProfessionService transaction; UI only sends recipe/mutation intent. New metadata must not alter old clients or stored player instances.

The older profession specification describes an unimplemented three-profession/level-60 foundation. This slice documents the current two-profession/skill-300 runtime rather than replacing it with that old proposal.
