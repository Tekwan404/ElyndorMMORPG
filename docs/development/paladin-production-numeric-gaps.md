# Paladin production numeric-contract audit

Audit date: 2026-10-01. Scope: shared player mechanics, not Arena-specific balance.
This is an implementation audit, not a new balance specification.

## Sources checked

- **T**: `content/talents/paladin.json`, exact node ID, English name, description and modifiers. Event-hook `values: [1, ...]` are hook/rank markers, not the missing effect magnitude.
- **A**: `content/abilities/paladin.json`, corresponding base ability cost, duration, cooldown and actions. Base ability values do not specify talent rank deltas.
- **D**: `docs/source-of-truth/gameplay/30_PALADIN_RUNTIME_AUDIT.md`, especially the class tables and `Missing numerical contracts — do not guess`.
- **R**: `src/Elyndor.Core/Combat/Paladin/PaladinHealingRuntime.cs`, `PaladinProtectionRuntime.cs`, `PaladinRetributionRuntime.cs`, and `Sessions/CombatSession.Paladin.cs` / `CombatSession.PaladinAbilityModifiers.cs`. Pure runtimes provide eligibility/state or accept configured numbers; that does not supply the missing balance value.

Repository search of `docs/` and `content/` found no additional Paladin-specific numeric contract. `content/drafts/paladin.json`, referenced by the older audit, is absent. Other classes' similarly named talents are not Paladin balance sources.

## Missing complete contracts

Every row was checked against T, A, D and R. Known base ability numbers are intentionally not substituted for missing talent bonuses.

| ID | Exact English name | What is missing | Known portion / corresponding ability |
| --- | --- | --- | --- |
| H-3-3 | Judgement of Wisdom | Mana restored per proc, proc chance/eligibility and personal ICD | Judgement debuff duration is 12 seconds; no numeric Mana proc contract |
| H-3-4 | Improved Lay on Hands | Cooldown reduction per rank; Mana restored to caster (amount or fraction) | LAY_ON_HANDS base values exist |
| H-4-2 | Improved Holy Shock | Cooldown reduction and critical-chance bonus per rank | HOLY_SHOCK / HOLY_SHOCK_OFFENSIVE base values exist |
| H-7-1 | Judgement of Light | Healing amount/scaling, proc cadence/chance, personal ICD and debuff duration | JUDGEMENT base values do not define this proc |
| H-8-3 | Last Light | Incoming-damage reduction percentage and duration | LAY_ON_HANDS target receives additional defense |
| P-1-4 | Improved Devotion Aura | Armor enhancement per rank and its flat/percent calculation basis | DEVOTION_AURA base effect is not the enhancement |
| P-2-1 | Bulwark | Block-chance increase and buff duration | Trigger chance is exactly 10/20/30%; insufficient to define the resulting buff |
| P-2-4 | Guardian's Favor | BLESSING_OF_PROTECTION cooldown reduction and duration increase per rank | Base ability values exist |
| P-3-4 | Consecrated Ground | CONSECRATION damage bonus and threat increase per rank | Base periodic damage values exist |
| P-4-4 | Shield of Faith | Absorb amount/scaling and shield duration | Trigger is HOLY_SHIELD ending |
| P-5-2 | Improved Avenger's Shield | AVENGERS_SHIELD damage bonus and cooldown reduction per rank | Base ability values exist |
| P-5-4 | Master of Sanctuary | BLESSING_OF_SANCTUARY enhancement; Mana return amount and proc/ICD rules | Incoming hits during the blessing are eligible in the description |
| P-6-2 | One-Handed Weapon Specialization | Damage and accuracy bonuses per rank | Exact equipment condition: one-handed weapon plus shield |
| P-8-1 | Unbreakable Bastion | Large-hit threshold as max-HP percentage, reduction magnitude/calculation and ICD | Pure protection runtime does not define these values |
| P-8-3 | Perfect Sanctuary | BLESSING_OF_SANCTUARY enhancement and owner-only block interaction magnitude/rules | Qualitative description only |
| R-3-4 | Improved Crusader Strike | CRUSADER_STRIKE cooldown reduction and critical-chance bonus per rank | Base ability values exist |
| R-4-4 | Righteous Verdict | JUDGEMENT damage and critical-chance bonuses per rank | Not the separate R-5-2 crit bonus |
| R-7-2 | Wrathful Vengeance | Critical-damage bonus per rank inside AVENGING_WRATH | Wrath duration exists; bonus does not |
| R-7-3 | Seal Mastery | SEAL_OF_COMMAND enhancement and JUDGEMENT-to-Vengeance maintenance/refresh amount/rules | Existing Vengeance cap/refresh primitive is not this talent's balance |

## Partial contracts / existing behavior gaps

| ID | Exact English name | Known and implemented portion | Missing or non-equivalent portion | Sources checked |
| --- | --- | --- | --- | --- |
| H-6-1 | Surge of Light | Rank 2 next FLASH_OF_LIGHT is instant | Rank 1 cast-time reduction; rank 2 Mana reduction; numeric proc-window contract | T/A/D/R |
| R-6-1 | The Art of War | Rank 2 next FLASH_OF_LIGHT is instant | Rank 1 cast-time reduction and both ranks' Mana reductions; numeric proc-window contract | T/A/D/R |
| R-5-2 | Fanaticism | JUDGEMENT critical-chance bonus 3/6/9 percentage points | Retribution damage threat-reduction magnitude | T/A/D/R |

## Known numbers implemented in the shared path

- P-1-1 **Toughness**: +2/4/6/8/10% of captured equipment armor, not total actor armor.
- P-5-3 **Ardent Defender**: 5/10/15% incoming-damage reduction only while HP is strictly below 35%; 35% is excluded.
- P-7-2 **Consecrated Protection**: 3/6% incoming-damage reduction during the actual Consecration runtime window.
- R-2-3 **Two-Handed Weapon Specialization**: 3/6/9% physical weapon-ability and autoattack damage with captured two-handed weapon category.
- R-1-3 **Conviction**: +1/2/3/4/5 critical-chance percentage points for offensive abilities and autoattacks, not healing.
- R-9-1 **Incarnation of Retribution**: actual Wrath effect lasts 16 seconds; exactly the first Verdict in **each successful Wrath activation** gets guaranteed crit. Snapshot resolution does not consume it; successful AbilityStarted does. The consumed marker resets on successful Wrath AbilityCompleted.
- R-8-3 **Divine Purpose**: the existing production runtime supplies the exact 12 Mana / 25% / 20-second values. Every third successful Judgement arms one Verdict. The pure resolver subtracts 12 Mana before admission (30 becomes 18); successful AbilityStarted consumes the armed state and ready effect. A per-player pending flag preserves the existing separate 25% post-hit Magical damage pipeline, consumed once on Verdict damage and cleared on completion even after dodge/miss. There is no post-hit Mana refund. Expiration gates both cost and bonus, including exactly 20 seconds. Snapshot queries and rejected casts do not consume the proc.

## Wiring and verification

`CombatSessionFactory` captures `derived.Equipment.ArmorFlat` and `derived.Equipment.MainHandWeaponCategory` into immutable `CombatParticipantDefinition.EquipmentArmor` / `MainHandWeaponCategory`, in both solo and additional/Arena-player factory paths. The assembler retains the participant capture.

`InitializePaladinLoadouts` now initializes each captured PvE player and the
mechanics-only Arena host, restoring the active context afterward. The shared
autoattack resolver combines Paladin damage and critical-chance modifiers with
the existing modifiers. No Arena-only equipment formula is introduced.

The focused production-path tests cover equipment-only Toughness, the strict
Ardent Defender threshold, Consecration expiration, two-handed weapon damage,
Conviction, repeated Divine Favor/Herald projections, zero-Mana free Holy Shock,
Surge of Light and two separate Wrath windows. These passed in the 318-case
targeted verification after wiring. Unknown numeric contracts above remain an
explicit blocker to claiming complete implementation of every Paladin talent.

After the R-8-3 cost fix, the focused `ArenaOtherClassProductionParityTests` suite
passes 45/45 with build successful. Added behavioral checks cover 18-Mana
Verdict admission (17 rejects without consumption), repeated snapshot reads,
exactly one discount and unchanged 25% Magical bonus, expiration immediately
before/at/after 20 seconds, and no bonus leak after a dodged successful cast.
The combined Paladin / owner-class production regression filter also passes
83/83 against the same build. The unknown 19 numeric contracts were not changed.
