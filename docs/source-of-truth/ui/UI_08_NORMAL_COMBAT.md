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
PLAYER / ENEMY HUD

ENEMY ART

BUFFS / DEBUFFS

CAST BAR

PLAYER / COMPANION PRESENTATION

MAIN ABILITY ROW (6)

UTILITY / ITEM ROW (2–4)

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

Party members are compact strip when applicable.

---

# 5. Ability Row

Main row = 6 abilities.

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

Tap target switches target.

No spatial battlefield/closest-target assumptions.

---

# 9. Buffs / Debuffs

Player buffs under player bars.
Enemy debuffs under enemy HP.

Show up to 6 important icons; overflow:

```text
+3
```

Tap/press icon via normal tap detail, not mandatory long-press.

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
