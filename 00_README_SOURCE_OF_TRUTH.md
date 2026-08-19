# Elyndor — MASTER Source of Truth v7.1

> Human entry point: `README.md`  
> Full index: `00_MASTER_PROJECT_INDEX.md`  
> Visual canon: `00_MASTER_UI_REFERENCE.md`  
> Full audit: `00_FULL_AUDIT_V7_1.md`

Этот каталог является текущей единой базой проекта Elyndor.

## Source precedence

```text
01–31 System Source of Truth
→ UI_01–UI_20
→ 00_MASTER_UI_REFERENCE.md
→ visual references
```

## Current document coverage

```text
Systems      01–31  COMPLETE
UI/UX        UI_01–UI_20 COMPLETE
Guild        COMPLETE
Raid Group   COMPLETE
Economy      COMPLETE
Trade        COMPLETE
Auction      COMPLETE
Dungeon      COMPLETE
Crafting     COMPLETE
Professions  COMPLETE
Visual canon INCLUDED
```

## Playable classes

```text
WARRIOR
ARCHER
MAGE
```

Future:

```text
PRIEST
ROGUE
```

## Key rules

- Level Cap = 60.
- 59 Talent Points at Level 60.
- Exactly 2 Talent Loadouts.
- Party max = 5.
- Raid max = 20, subgroups of 5.
- Guild default MemberLimit = 50.
- Inventory default = 40.
- Archer Companion tab exists only for Archer.
- `GOLD` is tradeable and Auction currency.
- `CRYSTAL` is non-tradeable.
- Auction = fixed-price buyout-only.
- Professions = Blacksmithing / Alchemy / Cooking.
- City is a Location.
- Bottom navigation = `МИР | ГЕРОЙ | ЛОКАЦИЯ | КВЕСТЫ | МЕНЮ`.
- Bottom navigation is hidden in Combat.
- Gear Score is display-only.
- Legendary/Unique may have unique visible appearance.
- Gameplay equipment and displayed appearance are separate.
- Current controls = Stun / Silence.
- No active Spirit / Block / Parry / CastSpeed.

## Engineering docs

- `00_DEVELOPMENT_STACK.md`
- `00_DEVELOPMENT_ROADMAP.md`
- `00_COMPATIBILITY_MATRIX.md`
- `00_CONTENT_AND_BALANCE_PROFILES.md`
- `00_MASTER_PROJECT_INDEX.md`
- `00_MANIFEST.md`

## UI docs

- `00_UI_UX_CONCEPT.md`
- `00_MASTER_UI_REFERENCE.md`
- `00_UI_REFERENCE_INDEX.md`
- `00_UI_PACK_SUMMARY.md`
- `UI_01_GLOBAL_GAME_SHELL.md` … `UI_20_GUILD.md`

## Visual references

```text
references/00_MASTER_VISUAL_REFERENCE_BOARD.jpg
references/PRIMARY_VISUAL_CANON/
references/STRUCTURE_ONLY/
```

`STRUCTURE_ONLY` must not define the Elyndor visual style.

## Development principle

```text
Build
→ Playtest
→ Refine
→ Expand
```

There is no separate reduced beta architecture. Closed Beta is a release/testing milestone in the roadmap, not a different system design.
