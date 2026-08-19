# Elyndor — MASTER Project Index

**Package:** MASTER Source of Truth v7.1  
**Purpose:** точка входа в полный проектный пакет.

---

# 1. Как пользоваться пакетом

```text
README.md
→ 00_README_SOURCE_OF_TRUTH.md
→ 00_MASTER_PROJECT_INDEX.md
→ нужная system specification
→ нужная UI specification
→ 00_MASTER_UI_REFERENCE.md
→ visual reference PNG
```

---

# 2. Gameplay / System Source of Truth

- `01_TIME_SYSTEM.md`
- `02_COMBAT_SYSTEM.md`
- `03_AFK_FARMING_SYSTEM.md`
- `04_WORLD_AND_LOCATIONS_SYSTEM.md`
- `05_CHARACTER_SYSTEM.md`
- `06_ATTRIBUTES_AND_STATS_SYSTEM.md`
- `07_RESOURCE_SYSTEM.md`
- `08_EFFECT_SYSTEM.md`
- `09_DAMAGE_AND_HEALING_SYSTEM.md`
- `10_ABILITY_SYSTEM.md`
- `11_PROGRESSION_SYSTEM.md`
- `12_CLASS_SYSTEM.md`
- `13_ITEM_EQUIPMENT_SYSTEM.md`
- `14_LOOT_SYSTEM.md`
- `15_MONSTER_AND_AI_SYSTEM.md`
- `16_TALENT_SYSTEM.md`
- `17_QUEST_SYSTEM.md`
- `18_BOSS_AND_WORLD_EVENT_SYSTEM.md`
- `19_CLASS_ROSTER_AND_CHARACTER_CREATION.md`
- `20_PARTY_SYSTEM.md`
- `21_COMPANION_AND_PET_SYSTEM.md`
- `22_WARRIOR_TALENT_TREE.md`
- `23_ARCHER_TALENT_TREE.md`
- `24_EQUIPMENT_SETS_LEVEL_5_30.md`
- `25_MAGE_TALENT_TREE.md`
- `26_CURRENCY_AND_ECONOMY_SYSTEM.md`
- `27_TRADE_AND_AUCTION_SYSTEM.md`
- `28_DUNGEON_SYSTEM.md`
- `29_CRAFTING_AND_PROFESSION_SYSTEM.md`
- `30_GUILD_SYSTEM.md`
- `31_RAID_GROUP_SYSTEM.md`

---

# 3. UI/UX Specifications

- `UI_01_GLOBAL_GAME_SHELL.md`
- `UI_02_WORLD_AND_LOCATION.md`
- `UI_03_HERO.md`
- `UI_04_INVENTORY_AND_ITEMS.md`
- `UI_05_CHARACTER_STATS.md`
- `UI_06_TALENTS.md`
- `UI_07_COMPANION.md`
- `UI_08_NORMAL_COMBAT.md`
- `UI_09_WORLD_BOSS_COMBAT.md`
- `UI_10_PARTY.md`
- `UI_11_QUESTS.md`
- `UI_12_CITY_LOCATION.md`
- `UI_13_MERCHANT.md`
- `UI_14_AUCTION.md`
- `UI_15_DUNGEON.md`
- `UI_16_CRAFTING_AND_PROFESSIONS.md`
- `UI_17_MENU.md`
- `UI_18_WALLET_AND_ECONOMY.md`
- `UI_19_SETTINGS_AND_SYSTEM_STATES.md`
- `UI_20_GUILD.md`

---

# 4. Engineering / Project docs

- `README.md` — human-friendly entry point.

- `00_DEVELOPMENT_ROADMAP.md`
- `00_DEVELOPMENT_STACK.md`
- `00_COMPATIBILITY_MATRIX.md`
- `00_CONTENT_AND_BALANCE_PROFILES.md`

---

# 5. UI master docs

- `00_UI_UX_CONCEPT.md`
- `00_MASTER_UI_REFERENCE.md`
- `00_UI_REFERENCE_INDEX.md`
- `00_UI_PACK_SUMMARY.md`

---

# 6. Validation

- `00_FULL_AUDIT_V7_1.md`
- `00_MANIFEST.md`

---

# 7. Visual references

- `references/00_MASTER_VISUAL_REFERENCE_BOARD.jpg`
- `references/PRIMARY_VISUAL_CANON/`
- `references/STRUCTURE_ONLY/`

---

# 8. Rule

Новые решения не должны тихо перезаписывать старые документы.

Если gameplay decision меняется:
1. обновить system document;
2. обновить dependent UI documents;
3. обновить compatibility/audit;
4. только затем обновлять visual reference.
