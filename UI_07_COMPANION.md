# Elyndor — UI/UX Specification 07 — Archer Companion

**Document:** `UI_07_COMPANION.md`  
**Status:** Approved foundation  
**Platform:** Telegram Mini App  
**Orientation:** Mobile Portrait First  
**Depends on:**
- `21_COMPANION_AND_PET_SYSTEM.md`
- `23_ARCHER_TALENT_TREE.md`
- `UI_03_HERO.md`

---

# 1. Назначение

Экран `СПУТНИК` существует только у Archer и управляет активным companion, доступными приручёнными companions и их archetype/ability presentation.

---

# 2. Visibility

```text
Warrior → no Companion tab
Mage → no Companion tab
Archer → Companion tab
```

Никаких disabled пустых вкладок для других классов.

---

# 3. Main Layout

```text
СПУТНИК

[large companion art/model]

Ночной Ветер
PREDATOR
Status: Active

HP / state
Role description

ABILITIES
[icon][icon][icon]

AVAILABLE COMPANIONS
[wolf][bear][raptor]...
```

---

# 4. Archetypes

Physical pet archetypes:

```text
ХИЩНИК   → DPS
СТРАЖ    → tank/protection
ЛОВЧИЙ   → control/debuff
```

Arcane branch uses Spirit Pet.

Physical and Spirit Pet bonuses are visually separated.

---

# 5. Switching

Companion switch only out of combat.

Tap companion:

```text
details
→ [ СДЕЛАТЬ АКТИВНЫМ ]
```

Server validates recovery/state/loadout.

---

# 6. Defeated State

If active pet defeated:

```text
ПОБЕЖДЁН
Восстановление: 00:08
```

Нет бесплатной кнопки мгновенного same-combat resummon.

---

# 7. Arcane Branch

When `Тайны Магии` active:

```text
physical pet → inactive
spirit pet → active
```

UI объясняет:

```text
Магический спутник активен из-за текущего билда.
```

При возврате physical build восстанавливается предыдущий physical pet без exploit/reset.

---

# 8. Taming / Collection

Future/active taming collection показывает:
- known/tamed creature;
- archetype;
- appearance;
- unavailable/locked state.

Не превращать Companion screen в Pokemon-like collection first; active pet remains main focus.

---

# 9. Abilities

Companion abilities используют тот же Ability system.

UI показывает:
- icon;
- name;
- cooldown if relevant;
- passive/active marker.

Manual combat controls описываются Combat UI.

---

# 10. Visual Reference

Использовать ранее созданный Archer companion reference:
- large pet art;
- archetype selector;
- ability row;
- collection strip;
- dark-fantasy frames.

---

# 11. Approved Decisions

1. Archer always has a companion when state allows.
2. One active companion.
3. Physical archetypes Predator/Guardian/Trapper.
4. Spirit Pet for Arcane branch.
5. Switch OOC only.
6. Defeated recovery visible.
7. Companion contributes materially but does not replace owner.
8. Ordinary gear does not directly display Pet Damage/Crit/AS stats.
