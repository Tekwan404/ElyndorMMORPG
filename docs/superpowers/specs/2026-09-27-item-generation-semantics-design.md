# Item Generation Semantics Design

## Goal

Make an item's stat-generation intent explicit without replacing Elyndor's existing content composition, itemization profiles, runtime DTOs, or persistence model.

## Audited current state

- The authored package contains 1,035 item entries across the base package and `content/items/*.json`.
- 872 authored entries are equipment; every one has a complete V2 affix-pool/count-profile configuration.
- The composed package contains 783 item definitions, including 625 equipment definitions.
- Every composed equipment definition currently uses procedural V2 itemization; there are no current fixed equipment definitions.
- 252 stable item IDs are authored twice as full-object replacements, primarily by the set-pass overlays. This is real authoring duplication, but it is an overlay/composition concern and does not justify a general template engine in this change.
- Equipment sets, affix pools, acquisition sources, and audit expansion are already correctly separated. The denormalized audit export remains intentionally unchanged.

## Model

Add `ItemGenerationMode` with two public values:

- `Fixed`: the definition's explicit stats are the complete result and no generated stat policy may be present.
- `Rolled`: the item is resolved through an approved random generation policy.

`GenerationMode` is orthogonal to `SetId`, uniqueness, economy, and acquisition. Existing equipment is authored as `Rolled`. New fixed equipment can carry explicit primary/secondary stats, weapon damage, armor, block values, set membership, and economy restrictions, but cannot carry random-affix or range configuration.

No `Generated / Named / Set` inheritance hierarchy is introduced. No template engine or second content compiler is introduced.

## Runtime compatibility

`ProceduralItemPolicy` uses `GenerationMode` as the authoritative gate. A rolled V2 item still requires the existing affix pool and affix-count profile. A fixed item never enters procedural generation even if malformed content reaches the policy.

Historical canonical packages that predate `generationMode` are upgraded by `GameContentPackageCodec` before deserialization. The upgrader infers `Rolled` only from the same generation configuration that previously enabled runtime generation; all other historical items become `Fixed`. This preserves published revision restore behavior.

Legacy primary-stat ranges remain supported as a rolled legacy shape for historical packages, but current bundled content must use explicit V2 rolled configuration.

## Validation

Validation owns the semantic rules:

- `Fixed` rejects item-level ranges, primary-stat ranges, affix pools, affix-count profiles, guaranteed random affixes, extra affix budget, naming policy, and non-default generation versions.
- `Rolled` must be equipment and must have either a complete V2 policy or a valid legacy primary-stat range policy.
- Partial or mixed generation policies are rejected.
- Equipment category validation remains centralized and additionally rejects a two-handed weapon category in an off-hand slot.
- Existing slot/category, weapon-damage, shield-block, set-reference, stack, and consumable rules remain authoritative.

The bundled content test requires every composed equipment item to declare `Rolled`, preventing accidental reliance on constructor/default inference in current authoring content.

## ItemDefinition structure

The positional `ItemDefinition` is not split into nested `Equipment`, `Generation`, `Economy`, and `Consumable` records in this change. That migration would touch every runtime/API/admin consumer while providing no behavioral benefit for the immediate ambiguity. The new mode and centralized predicates create a safe seam for a later serialization-compatible grouping migration.

## Non-goals

- No balance or item-stat changes.
- No item IDs, set IDs, loot sources, or economy changes.
- No templates or profile-ID proliferation.
- No unique item effect runtime.
- No changes to the denormalized analysis export beyond exposing generation mode.

## Verification

- Unit tests for fixed/rolled validation and runtime generation gating.
- Compatibility test for historical payloads without `generationMode`.
- Composed-content test proving every current equipment definition is explicitly rolled and remains procedurally enabled.
- Content validator, Release build, focused tests, then the complete backend test suite.
