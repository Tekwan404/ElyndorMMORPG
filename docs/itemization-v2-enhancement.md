# Itemization V2: birth quality and enhancement

## Source of truth

**Intrinsic item properties are immutable after item creation.**

The persisted generated instance is the historical record of the item that entered the economy. The following fields describe that birth event and are never recomputed by forge progression:

- rarity and item level;
- stars and roll quality;
- Perfect status and Perfect origin;
- generation min/max/step envelopes;
- generated prefix, suffix and display name;
- intrinsic item power.

A 5-star item is not automatically Perfect. Stars classify the birth roll: 1★ below 30%, 2★ from 30%, 3★ from 50%, 4★ from 70%, and 5★ from 90%. Perfect is a separate birth-only condition requiring the generated instance to hit its actual maximum conditions.

## Enhancement

Enhancement is post-acquisition player investment and is stored as `EnhancementLevel` from +0 through +5. Each level adds 2% (maximum +10%) to structural equipment stats only.

Structural enhancement can affect armor, weapon damage, shield block value, and structural spell power on magic weapons/foci. It does not scale random affixes such as Strength, Critical Chance, Block Chance, Accuracy, Dodge, penetration or other rolled secondary stats.

Enhancement never mutates Stars, RollQuality, Perfect, PerfectOrigin, rolled affixes, generation envelopes, generated name or intrinsic item power. UI/comparison code must keep intrinsic item power separate from final enhanced power.

## Reforge

Reforge may replace one eligible non-guaranteed affix. It does not rerun item classification. The replacement roll stays near the normalized quality of the replaced affix (currently ±10 percentage points) and cannot create an exact maximum roll. Therefore reforge cannot create Perfect or increase/decrease the item's Stars or birth RollQuality.

The selected reforge affix is the only intrinsic-looking field allowed to change after creation; the original birth classification remains canonical.

## Historical items

Existing persisted birth metadata is preserved. Historical repair may reconstruct malformed generation ranges, but the repair must restore the stored Stars, RollQuality, Perfect state, intrinsic power and generated naming rather than reclassifying the item.

Existing `EnhancementLevel` is preserved. If a legacy item's original natural Stars cannot be recovered from reliable historical data, migration must not invent a lower or higher natural star value.

## Compatibility

The canonical API is `/api/v1/inventory/enhancement`. Legacy `star-upgrade` endpoints/services may remain temporarily as compatibility aliases, but they delegate to enhancement semantics and must never mutate Stars.
