# Shield / Block Strength V2

Status: implementation slice on top of the already merged Shield & Block V2 runtime.

## Goal

Make shield defense scale into progression without turning Strength itself into generic Armor.
The intended split is:

- **Armor** comes from equipment and approved Armor modifiers;
- **Block Chance** comes from an equipped shield plus approved talents/effects;
- **Block Value** comes from the shield, Strength and approved talents/effects;
- Strength alone never creates block and never grants Armor.

This keeps a shield Warrior materially tougher than a two-handed / Berserker setup while preserving Strength as a shared Warrior offensive stat.

## Authoritative block formula

For a valid equipped shield profile:

```text
StrengthBlockValue = max(0, Strength) * 0.75

FinalBlockChance = clamp(
    ShieldBlockChance + TalentBlockChance,
    0%,
    60%
)

FinalBlockValueMin = max(
    0,
    ShieldBlockValueMin + StrengthBlockValue + TalentBlockValueFlat
)

FinalBlockValueMax = max(
    FinalBlockValueMin,
    ShieldBlockValueMax + StrengthBlockValue + TalentBlockValueFlat
)
```

`blockChancePercent` remains a **final percentage-point input** from equipment. It is not reinterpreted as a rating in this slice.

Without a valid shield profile the final Block Chance and Block Value are zero regardless of Strength or Guardian talent bonuses.

## Damage order

The merged Shield & Block V2 order remains unchanged:

```text
raw physical damage
-> armor / penetration mitigation
-> damage modifiers
-> minimum damage
-> equipment block
-> temporary shield/barrier absorption
-> HP
```

Full equipment blocks remain valid. Unblockable attacks continue to bypass equipment block. The existing `60%` Block Chance cap remains the probability safety cap.

## Armor boundary

Do not restore `ArmorPerStrength` or `ArmorPerStamina` into the runtime formula. Strength and Stamina do not generate physical Armor by themselves.

The current level-scaled Armor formula and its `60%` reduction cap stay unchanged. Tank-vs-Berserker separation is created by shield Armor, block frequency and Strength-scaled Block Value rather than by a class-global Strength-to-Armor conversion.

## Shield balance pass

Base shield values after this slice:

| Shield | Level | Armor | Block Chance | Block Value |
| --- | ---: | ---: | ---: | ---: |
| Щит Пограничника | 2 | 30 | 5% | 6–10 |
| Щит Железного Дозора | 6 | 60 | 6% | 14–22 |
| Щит Серого Волка | 10 | 105 | 7% | 24–36 |
| Оплот Хранителя Глубин | 16 | 230 | 9% | 45–65 |
| Щит Первого Стража | 18 | 260 | 9% | 45–65 |
| Щит Чёрного Бастиона | 23 | 360 | 10% | 60–85 |

Random item affixes and Guardian talents may add to these values through the existing authoritative stat pipeline.

## Compatibility

This slice deliberately preserves:

- Shield & Block V2 combat events and UI telemetry;
- Guardian Rage/Threat reactions to successful blocks;
- unblockable attack support;
- current level-scaled Armor mitigation;
- current Guardian talent topology and Russian descriptions;
- generic Magic Resistance only; no per-element resistance system is introduced.
