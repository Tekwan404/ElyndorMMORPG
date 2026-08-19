# Elyndor — Companion & Pet System Specification

**Document:** 21_COMPANION_AND_PET_SYSTEM.md  
**System:** Companion / Pet  
**Status:** Foundation / Source of Truth  
**Version:** 1.0

---

# 1. Назначение

Companion System определяет постоянного боевого спутника персонажа.

На текущем roster обязательный companion имеет Archer.

Companion не создаёт отдельный Combat Engine.

Он использует:
- Time;
- Combat;
- Stats;
- Resource;
- Effect;
- Damage;
- Ability;
- Threat;
- World;
- Talent.

---

# 2. Archer Rule

Лучник **всегда имеет одного активного спутника**, если персонаж находится в состоянии, где companion разрешён.

Marksman:
- PHYSICAL_PET остаётся вспомогательным.

Beast Master:
- PHYSICAL_PET является значимой частью build (~25–35% общей эффективности).

Arcane Archer:
- PHYSICAL_PET заменяется на SPIRIT_PET.

---

# 3. Companion Tags

```text
PHYSICAL_PET
SPIRIT_PET
```

Physical archetypes:

```text
PREDATOR
GUARDIAN
TRAPPER
```

Talent/effect с `PHYSICAL_PET` не действует на `SPIRIT_PET`.
Talent/effect с `SPIRIT_PET` не действует на physical archetypes.

---

# 3.1 Physical Companion Collection

Archer всегда имеет как минимум одного доступного PHYSICAL_PET.

В дальнейшем персонаж может приручить/открыть несколько физических зверей, но активен одновременно только один:

```text
OwnedPhysicalCompanions: 1..N
ActivePhysicalCompanionId: 0..1
MaxActiveCompanions = 1
```

Смена активного физического зверя:
- только вне Combat;
- не создаёт второго active companion;
- не сбрасывает cooldown/recovery;
- сохраняет индивидуальный CompanionId и cosmetic identity зверя.

При выборе Arcane loadout физический зверь не удаляется из коллекции. Он становится inactive, а боевой слот занимает derived `SPIRIT_PET`. При возврате на physical loadout восстанавливается последний выбранный физический зверь.

`SPIRIT_PET` не является приручённым физическим зверем и не занимает его permanent collection slot.

---

# 3.2. Starting Companion Guarantee

При создании `ARCHER` Companion System атомарно выдаёт минимум одного starter `PHYSICAL_PET` через `StartingCompanionProfileId`.

Default archetype для стартового профиля:

```text
PREDATOR
```

Конкретный species/visual является content data. Поэтому Archer никогда не появляется в мире в невалидном состоянии «класс требует companion, но companion отсутствует».

---

# 4. Entity

```text
CompanionInstance
├── CompanionId
├── OwnerCharacterId
├── CompanionDefinitionId
├── CompanionTag
├── Archetype
├── LifeState
├── CurrentLocationId
├── CombatSessionId, optional
├── AIProfileId
├── StateVersion
└── Metadata
```

---

# 5. Ownership

Один Companion имеет ровно одного OwnerCharacterId.

Текущий Archer:
```text
MaxActiveCompanions = 1
```

---

# 6. Stats / Scaling

Companion имеет собственные Final Stats, но базируется на:

```text
Companion Base Profile
+ Level
+ Owner Scaling Profile
+ Talent Modifiers
+ Active Effects
```

Обычная экипировка не даёт прямые:
- Pet Damage +X%;
- Pet Crit +X%;
- Pet AttackSpeed +X%.

Рост pet через gear владельца проходит через owner scaling.

---

# 7. Physical Pet

PHYSICAL_PET получает физический профиль.

## Predator
- damage;
- bleed;
- execute.

## Guardian
- threat;
- owner protection;
- damage interception.

## Trapper
- Silence;
- AttackSpeed reduction;
- Accuracy reduction;
- utility.

Slow/Root/Fear не используются, пока не поддержаны Effect/Combat.

---

# 8. Spirit Pet

SPIRIT_PET:
- Magical Damage;
- SpellPower-derived scaling;
- magic effects;
- support/control utility.

Spirit не получает Physical Pet talents.

---

# 9. Combat Participation

При входе Owner в Combat:
- active companion присоединяется как owned Combat participant;
- получает ту же CombatSessionId;
- выбирает target через Companion AI;
- abilities проходят Ability System;
- damage проходит Damage System;
- effects проходят Effect System;
- threat учитывается Combat Threat rules.

---

# 10. Target

По умолчанию companion стремится атаковать current valid target Owner.

AI может выбрать другое действие, если archetype/ability profile требует:
- защитить Owner;
- применить utility;
- taunt/Threat action;
- heal/shield owner, если content это разрешает.

---

# 11. Commands

Pet command — AbilityDefinition владельца или companion command request.

Пример:

```text
Команда: Фас
```

валидируется сервером:
- active pet exists;
- pet alive;
- correct CompanionTag/Archetype;
- cooldown;
- owner resource;
- target valid.

---

# 12. Companion Death

При CurrentHP = 0:

```text
LifeState = DEFEATED
```

Companion:
- не умирает навсегда;
- прекращает Auto Attack/abilities;
- не может быть бесплатно повторно призван в том же CombatSession;
- остаётся недоступным до recovery или специальной revive ability/talent.

---

# 13. Recovery After Combat

После завершения CombatSession:

```text
DEFEATED
→ RECOVERING
→ ACTIVE
```

BaseRecoveryTime:

```text
10 seconds
```

После recovery companion возвращается с:

```text
CurrentHP = MaxHP
```

В Safe Territory сервис/отдых может восстановить его мгновенно.

Число 10 sec является базовым balance profile и может изменяться data-driven.

---

# 14. Revive in Combat

Базово отсутствует бесплатный resummon.

Будущая/конкретная Ability может:
- восстановить DEFEATED pet;
- иметь cooldown/resource cost;
- вернуть pet с заданным % HP.

Такой revive обязан быть explicit content.

---

# 15. Owner Death

Если Owner становится DEAD:
- companion прекращает offensive actions;
- покидает активный Combat при завершении owner participation;
- не продолжает бесконечно фармить после смерти владельца.

При respawn Owner companion приводится к согласованному post-death state; если был DEFEATED — recovery применяется обычным способом.

---

# 16. Logout

Companion persistence следует Owner world presence.

Logout не создаёт отдельный pet farming loop.

Если Owner остаётся в реальном Combat/offline world state, companion может продолжать только действия, которые разрешены Character/Combat offline policy.

AFK Farming не симулирует полноценный companion combat.

---

# 17. Restart

Persistent fields сохраняются.

Активный runtime combat companion восстанавливается согласно Combat restart policy.

Нельзя:
- дублировать companion;
- выдавать второго active pet;
- повторно применять одноразовые effects из-за restart.

---

# 18. Talent Integration

Talent modifier может фильтровать:

```text
CompanionTag
Archetype
AbilityTag
OwnerClassId
```

Например:

```text
PHYSICAL_PET + PREDATOR
SPIRIT_PET
PHYSICAL_PET + GUARDIAN
```

---

# 19. Equipment

Gear владельца влияет на companion только через ScalingProfile.

Редкие будущие Unique/Legendary effects могут менять pet mechanic, но не должны превращать обычную itemization в отдельные Pet Damage проценты.

---

# 20. Invariants

1. Archer имеет максимум одного active companion.
2. Companion имеет одного owner.
3. PHYSICAL_PET и SPIRIT_PET bonuses не пересекаются без explicit multi-tag effect.
4. DEFEATED pet не может бесплатно вернуться в том же бою.
5. Companion использует общий Combat Engine.
6. Gear → owner stats → pet scaling.
7. Pet combat events имеют SourceId = CompanionId и OwnerCharacterId context.


---

# Talent Loadout Switch Safety

Переключение Archer loadout не может использоваться как revive/heal exploit.

- `DEFEATED` / `RECOVERING` state сохраняется по CompanionId.
- Переход `PHYSICAL_PET → SPIRIT_PET → PHYSICAL_PET` не завершает recovery физического зверя досрочно.
- Если активный derived Spirit был DEFEATED, переключение loadout не создаёт бесплатного нового Spirit с полным HP до завершения применимого recovery.
- Companion cooldowns, которые должны переживать смену loadout, хранятся независимо от active presentation profile.
