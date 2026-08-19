# Elyndor — UI/UX Specification 06 — Talents

**Document:** `UI_06_TALENTS.md`  
**Status:** Approved foundation  
**Platform:** Telegram Mini App  
**Orientation:** Mobile Portrait First  
**Depends on:**
- `UI_03_HERO.md`
- `16_TALENT_SYSTEM.md`
- `22_WARRIOR_TALENT_TREE.md`
- `23_ARCHER_TALENT_TREE.md`
- `25_MAGE_TALENT_TREE.md`

---

# 1. Назначение

Экран `ТАЛАНТЫ` даёт мобильный интерфейс для 96-node class tree, двух сохранённых loadout и гибридных билдов.

Главная задача — не уменьшить всё дерево до нечитаемой миниатюры.

---

# 2. Top Bar

```text
ТАЛАНТЫ
Build 1 | Build 2
Очки: 18
```

Active loadout обозначается явно. Переключение loadout доступно только при разрешённом server state.

---

# 3. Branch Navigation

У класса три ветки.

Recommended mobile pattern:

```text
[ Пламя ] [ Тайная ] [ Лёд ]
```

или соответствующие Warrior/Archer branch names.

На экране одновременно детально показывается **одна выбранная ветка**.

Допустим отдельный overview mode с тремя колонками, но он не заменяет основной readable branch view.

---

# 4. Tree Layout

Ветка — вертикальное дерево:

```text
Tier 0
 ● ─ ● ─ ●

Tier 5
     │
 ● ─ ●

Tier 10
...
```

Связи visible.
Locked tiers затемнены.
Prerequisite line показывает зависимость.

---

# 5. Talent Node States

```text
LOCKED
AVAILABLE
LEARNED
MAXED
PREREQUISITE_MISSING
NO_POINTS
```

Node показывает icon + rank:

```text
2/3
1/1
```

Не помещать длинный текст прямо в node.

---

# 6. Talent Detail

Tap node → bottom sheet:

```text
ОГНЕННАЯ КОМЕТА
1/1

Описание
Требования
Что изменится на следующем rank

[ ИЗУЧИТЬ ]
```

Для maxed:

```text
МАКСИМАЛЬНЫЙ РАНГ
```

---

# 7. Point Spend

Tap `Изучить` отправляет server request.

После confirmation:
- point count обновляется;
- node state меняется;
- connected nodes/tier recalculated;
- new ability toast/reveal if needed.

Не применять talent optimistic-authoritatively.

---

# 8. Respec

Respec относится к выбранному loadout.

Flow:

```text
[ СБРОСИТЬ БИЛД ]
→ cost preview
→ confirm
→ Economy spend if required
→ Talent respec
```

Первый/тестовый free-respec policy задаётся Economy/Talent content, не UI.

---

# 9. Loadout Switch

Build switch:

```text
Build 1 ↔ Build 2
```

Запрещён:
- IN_COMBAT;
- DEAD;
- casting;
- location transition;
- другие states из Talent System.

Cooldowns/resource ICDs не сбрасываются.

При Archer Arcane loadout UI сразу меняет resource representation Focus→Mana после authoritative update.

---

# 10. Class-Specific HUD Hooks

Mage:
- Fire: Fireball crit streak / Heat Limit.
- Arcane: Arcane Charges.
- Frost: Frostbite/Deep Freeze.

Archer:
- branch identity;
- pet/Spirit Pet implications.

Warrior:
- tank/berserker/commander identity.

Эти hooks описываются в Combat UI, а Talent screen объясняет, что talent добавляет.

---

# 11. Hybrid Builds

UI не заставляет игрока выбрать одну specialization.

Можно тратить очки в нескольких branches.

Branch tab header показывает:

```text
Пламя 31
Тайная 18
Лёд 10
```

чтобы гибридный build был понятен.

---

# 12. Visual Reference

Основной reference:

```text
references/10_mage_talents.png
```

Но production mobile view делает выбранную ветку крупнее и читабельнее, чем three-column AI reference.

---

# 13. Approved Decisions

1. Exactly 2 saved loadouts.
2. 59 points at level 60.
3. 3 branches per class.
4. 96 nodes per class.
5. Mobile default = one branch focused.
6. Hybrid builds allowed.
7. Node detail через bottom sheet.
8. Respec selected loadout only.
9. Companion/Talent interactions remain server-authoritative.
