# Elyndor — UI/UX Specification 08 — Normal Combat

**Document:** `docs/source-of-truth/ui/UI_08_NORMAL_COMBAT.md`
**Status:** Approved foundation  
**Platform:** Telegram Mini App  
**Orientation:** Mobile Portrait First  
**Depends on:**
- `docs/source-of-truth/gameplay/02_COMBAT_SYSTEM.md`
- `docs/source-of-truth/gameplay/10_ABILITY_SYSTEM.md`
- `docs/source-of-truth/gameplay/08_EFFECT_SYSTEM.md`
- `docs/source-of-truth/gameplay/09_DAMAGE_AND_HEALING_SYSTEM.md`
- `docs/source-of-truth/ui/UI_01_GLOBAL_GAME_SHELL.md`

---

# 1. Назначение

Normal Combat UI — основной moment-to-moment gameplay экран. Во время боя global bottom navigation скрывается, а combat state получает весь доступный нижний action space.

---

# 2. Screen Structure

```text
BATTLEFIELD: ENEMY BACK CENTER / PARTY IN TWO PLANES

HP / RESOURCES ATTACHED TO EACH ACTOR

BUFFS / DEBUFFS

CAST BAR

PLAYER / COMPANION PRESENTATION

CIRCULAR ABILITIES (5 COLUMNS × 2 ROWS, PAGED IF MORE THAN 10)

[ ПОКИНУТЬ БОЙ ]
```

---

# 3. Enemy HUD

Показывает:
- name;
- level/type;
- HP current/max;
- important debuffs;
- cast/channel;
- target markers.

Не показывать неподдерживаемые distance/facing mechanics.

---

# 4. Player HUD

Показывает:
- HP;
- Rage/Focus/Mana;
- relevant buffs;
- combat status.

У каждого участника одна панель имени, HP и ресурса рядом с фигурой; числа находятся внутри полос. Отдельный верхний HUD не дублирует эти данные. Локальный герой расположен по центру переднего плана, союзники — по бокам в два плана, противник — выше по центру. Агро меняет подсветку, но не переставляет участников. Это визуальная композиция без distance/facing mechanics. Резервные участники доступны через компактную полосу.

---

# 5. Ability Row

Панель содержит 5 столбцов и 2 строки круглых кнопок с иконками, без постоянных подписей имени и стоимости. Минимальная область нажатия — 44px. Если умений больше 10, доступны страницы; никакое умение не скрывается окончательно. Название, стоимость и описание доступны удержанием или клавиатурной инспекцией.

States:
```text
READY
COOLDOWN
NO_RESOURCE
INVALID_TARGET
SILENCED
STUNNED
CASTING
QUEUED
PROC_READY
```

Cooldown number + radial visual.
Queue window 0.5s represented subtly.

Short tap uses an available ability. Press-and-hold for 500ms opens its name and description without casting; this remains available while the ability is on cooldown or lacks resources.

---

# 6. GCD / Cast

Global Cooldown = 1.5s.

Cast bar directly above abilities.

Player cast and enemy cast must be visually distinct.

Stun/Silence overlays should explain why ability unavailable.

---

# 7. Autoattack

Autoattack is server-driven weapon attack.

UI may show subtle swing/next-auto indicator, but not required as a giant timer.

AttackSpeed affects interval; no manual movement.

---

# 8. Multi-target

When multiple enemies exist:

```text
horizontal target strip
```

Tap target switches target. Полоса занимает отдельный верхний ряд и не перекрывает панель выбранного врага.

No spatial battlefield/closest-target assumptions.

---

# 9. Buffs / Debuffs

Player buffs under player bars.
Enemy debuffs under enemy HP.

Show up to 6 important icons; overflow:

```text
+3
```

Tap an effect icon to open its details; this uses a normal tap and does not require a long-press.

The detail shows the effect name, description, stack count when above one, and remaining duration. The icon uses the effect/ability art when available and the existing SVG glyph fallback otherwise.

---

# 10. Companion in Combat

Archer:
- compact pet portrait;
- HP/state;
- limited ability/status indicators.

Pet does not receive a massive second-player HUD.

---

# 11. Combat Log

Collapsed by default.

One-line recent event area optional.

Tap:
```text
→ expanded combat log sheet
```

Не занимает основной combat viewport постоянно.

---

# 12. Leave Combat

`ПОКИНУТЬ БОЙ` обязательна.

Server validates whether encounter can be escaped.
Button may require confirmation for boss/dungeon.

---

# 13. Damage Feedback

Floating numbers controlled:
- big crit;
- heal;
- miss;
- block not used;
- periodic ticks aggregated/less prominent.

Не спамить экран каждым DoT tick одинакового размера.

---

# 14. Class Hooks

Mage:
- Fire streak/Heat Limit;
- Arcane Charge diamonds;
- Frostbite target stacks.

Warrior:
- Rage prominence.

Archer:
- Focus/Mana switch;
- companion state.

UI reads these as combat projections.

---

# 15. Victory / Defeat

Victory:
```text
Combat
→ Reward Result
→ Continue
→ Location
```

Defeat:
```text
Combat
→ Death/Respawn flow
```

Reward screen уже определён в UI_01.

---

# 16. Approved Decisions

1. Bottom nav hidden in combat.
2. Main abilities = 6.
3. Secondary utility = 2–4.
4. Cast bar above skill row.
5. Leave Combat visible.
6. Multi-target = target strip.
7. Combat log collapsed.
8. No movement/distance/facing UI.
9. Companion compact.
10. Server-authoritative skill states.
