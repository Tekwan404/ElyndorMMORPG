# Elyndor — UI/UX Specification 19 — Settings & System States

**Document:** `UI_19_SETTINGS_AND_SYSTEM_STATES.md`  
**Status:** Approved foundation  
**Platform:** Telegram Mini App  
**Orientation:** Mobile Portrait First  
**Depends on:**
- `UI_01_GLOBAL_GAME_SHELL.md`
- `UI_02_WORLD_AND_LOCATION.md`
- `UI_03_HERO.md`

---

# 1. Назначение

Settings controls presentation/notifications and defines consistent UX for loading, reconnect, maintenance and errors.

---

# 2. Settings Sections

```text
ГРАФИКА
Анимация персонажа
Атмосферные эффекты
Reduced Motion / system

УВЕДОМЛЕНИЯ
World Boss
World Events
Travel, future

ИНТЕРФЕЙС
Haptics
Combat numbers
Combat log default

СИСТЕМА
Language, future
Help
Version
```

---

# 3. Performance

Atmospheric effects ON/OFF.
Character animation ON/OFF.
Later LOW/MEDIUM/HIGH quality possible.

---

# 4. Loading

Full-screen only:
- initial bootstrap;
- major transition;
- recovery.

Normal navigation uses skeleton/inline loader.

---

# 5. Reconnect

Banner:
```text
Соединение потеряно
Переподключение...
```

Actions disabled where unsafe.
After reconnect request authoritative snapshot.

---

# 6. Maintenance

```text
ТЕХНИЧЕСКИЕ РАБОТЫ
Сервер временно недоступен.
```

Do not loop login errors endlessly.

---

# 7. Version Mismatch

If client stale:
```text
Доступно обновление
[ПЕРЕЗАГРУЗИТЬ]
```

---

# 8. Action Errors

Toast/inline:
- insufficient Gold;
- invalid state;
- item unavailable;
- boss unavailable;
- party changed.

No raw exception text.

---

# 9. Approved Decisions

1. Effects can be disabled.
2. World Boss notifications configurable.
3. Reconnect never silently logs player out.
4. Fullscreen loaders are rare.
5. System errors have safe recovery CTA.
