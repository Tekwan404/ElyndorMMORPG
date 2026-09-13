# Shield / Block Rework — working design

Status: draft implementation branch. This document intentionally does not change Guardian talent content.

## Goals

- Separate physical armor, block chance and block value into independent defensive layers.
- Make a shield a meaningful armor source so a shield Warrior is materially tougher than a two-handed / Berserker setup with comparable gear.
- Scale block value with Strength so fixed low block values do not become obsolete as incoming damage grows.
- Keep generic `MAGIC_RESISTANCE`; do not add per-element resistances in this slice.
- Keep the Guardian talent rework isolated. Talents may later modify the mechanics exposed here, but this branch must not rewrite the Guardian talent tree or its content.

## Armor

Physical mitigation is level-scaled against the attacker:

```text
MitigationConstant = 400 + 85 * AttackerLevel
ArmorReduction = Armor / (Armor + MitigationConstant)
ArmorReduction = min(ArmorReduction, 75%)
PhysicalDamageAfterArmor = PhysicalDamage * (1 - ArmorReduction)
```

The attacker's level is authoritative. The same armor is therefore more effective against lower-level enemies and less effective against higher-level bosses.

Reference at attacker level 63 (`MitigationConstant = 5755`):

| Armor | Reduction |
| ---: | ---: |
| 2,000 | ~25.8% |
| 4,000 | ~41.0% |
| 5,755 | 50.0% |
| 8,000 | ~58.2% |
| 12,000 | ~67.6% |
| 17,265 | 75.0% cap |

This curve keeps armor useful over a long progression window while still providing a hard safety cap.

## Block terminology

Do not conflate these values:

- **Block rating**: a rating/stat that contributes to the probability that an eligible physical hit is blocked.
- **Block chance**: the final percentage probability after rating/base/talent/item modifiers are resolved.
- **Block value**: the amount of damage removed when a block succeeds.

Block chance and block value are independent. Increasing block rating must not directly increase block value, and increasing block value must not silently increase block chance.

### First-pass conversion

```text
FinalBlockChance = BlockRating / 0.30
FinalBlockChance = clamp(FinalBlockChance, 0%, 100%)

StrengthBlockValue = Strength * 0.75
FinalBlockValueMin = ShieldBlockValueMin + StrengthBlockValue
FinalBlockValueMax = ShieldBlockValueMax + StrengthBlockValue
```

The `0.30 rating = 1 percentage point` calibration deliberately fits the current item-power scale: shield rating is stored in small decimal units and remains expensive in the item budget.

During this branch the persisted/content names `BlockChancePercent` and `BLOCK_CHANCE` are legacy compatibility names. In the character stat pipeline their value is treated as **block rating**, and only `CharacterStats.BlockChance` is the final resolved percentage. A later schema/content migration can rename the legacy identifiers without changing this gameplay rule.

## Block value target model

The implemented first-pass model is:

```text
FinalBlockValue = ShieldBlockValue
                + ItemFlatBlockValue
                + StrengthContribution
                + approved talent/effect modifiers (future integration)
```

Strength contributes to block value only while a valid shield block profile exists. Strength does not grant Armor by itself and does not create block for a shieldless Berserker.

The current coefficient is `0.75 BlockValue per 1 Strength`; it is isolated in `ShieldBlockFormula` and covered by tests so it can be tuned independently from the damage pipeline.

## Shield identity

A shield provides four distinct defensive axes:

1. meaningful `Armor`;
2. block rating, which resolves into final block chance;
3. `BlockValue`;
4. approved defensive affixes such as Strength, Stamina and generic Magic Resistance.

The first balance pass uses roughly 5% base block chance at level 2, 6–7% through early/mid progression and 9–10% on late/legendary shields before additional rolled rating. Shield Armor and base BlockValue were raised at the same time so successful blocks remain visible at current damage values.

Higher-rarity shields may later receive one item proc / special effect through the item-effect system. Those procs are deliberately outside this first core-formula commit and must not be implemented as Guardian talent special cases.

## Damage order

The existing authoritative order remains:

```text
raw physical damage
-> armor / penetration mitigation
-> damage modifiers
-> minimum damage
-> equipment block
-> temporary barrier/shield absorption
-> HP
```

Equipment block and temporary barrier absorption are different mechanics and must continue to emit different combat feedback/events.

## Parallel development boundary

This branch must not modify Guardian talent JSON, talent descriptions, talent-tree topology or Guardian-specific runtime resolvers while the separate Guardian talent rework is in progress. Integration should happen only after both branches are reviewable and conflict-checked.
