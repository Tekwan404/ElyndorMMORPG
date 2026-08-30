Elyndor — Effect System Specification

Document: docs/source-of-truth/gameplay/08_EFFECT_SYSTEM.md
System: Effects
Status: Foundation / Source of Truth
Version: 0.4

1. Назначение

Effect System определяет правила временных и постоянных эффектов, применяемых к персонажам, NPC, монстрам и другим игровым сущностям.

Система охватывает:

buffs;
debuffs;
Damage over Time (DoT);
Healing over Time (HoT);
stat modifiers;
conditional modifiers;
control effects (stun, silence);
shields и другие защитные эффекты;
diminishing returns на control effects для боссов и элитных мобов.

Effect System не определяет:

конкретные формулы урона;
конкретные формулы лечения;
конкретные значения tick damage/healing;
конкретные cooldowns способностей;
классовые деревья;
лут;
экономику;
AI;
UI;
визуализацию эффектов.

Effect System предоставляет Combat System, Stats System, Resource System и Character System механизм применения временных изменений состояния.

2. Основной принцип

Все эффекты являются серверными данными.

Клиент может отображать активные эффекты, но не является источником истины.

Сервер определяет:

момент применения эффекта;
момент истечения эффекта;
момент каждого tick;
изменение характеристик под действием эффекта;
stacking behavior;
dispel rules;
diminishing returns;
interaction с другими системами.

Клиент может запросить применение эффекта через использование способности, предмета или другого разрешённого действия, но итоговое решение принимает сервер.

3. Effect Entity

Каждый эффект концептуально представляется как сущность:

Effect
  ├── EffectId
  ├── EffectType
  ├── SourceId
  ├── SourceType
  ├── TargetId
  ├── AppliedAt
  ├── ExpiresAt
  ├── Duration
  ├── RemainingDuration
  ├── TickInterval
  ├── NextTickAt
  ├── TicksRemaining
  ├── Stacks
  ├── MaxStacks
  ├── ApplicationPriority
  ├── SnapshotData
  ├── IsDynamic
  ├── DispelCategory
  ├── DR_Category
  ├── Version
  └── Metadata

Конкретная техническая реализация будет определена позднее.

4. Основные поля эффекта

### EffectId

Уникальный идентификатор конкретного применённого эффекта.

### EffectType

Тип эффекта из утверждённого набора.

### SourceId

Идентификатор источника эффекта:

персонаж;
NPC;
монстр;
объект мира;
скрипт;
зона;
мировое событие.

### SourceType

Категория источника.

### TargetId

Идентификатор цели эффекта.

### AppliedAt

Server Time в момент применения эффекта.

### ExpiresAt

Server Time, когда эффект должен истечь.

### Duration

Первоначальная длительность эффекта в секундах.

### RemainingDuration

Оставшееся время эффекта.

Используется для восстановления состояния.

### TickInterval

Интервал между tick для DoT/HoT эффектов.

### NextTickAt

Server Time следующего tick.

### TicksRemaining

Оставшееся количество tick.

### Stacks

Текущее количество стаков эффекта.

### MaxStacks

Максимальное допустимое количество стаков.

### ApplicationPriority

Числовой приоритет применения эффекта.

Используется для определения порядка применения при одновременном применении нескольких эффектов.

Default = 0 для большинства эффектов.

Более высокий приоритет применяется раньше.

### SnapshotData

Зафиксированные параметры, использованные при применении эффекта.

### IsDynamic

Флаг, указывающий, пересчитывается ли эффект динамически.

### DispelCategory

Категория для dispel/cleanse механик.

### DR_Category

Категория diminishing returns для control effects.

Определяет, как эффект взаимодействует с DR системой.

Примеры DR категорий Базовое правило:

Stun
Silence

Отложенные DR категории (не поддерживаются в текущей модели):

Root
Slow
Disorient

Конкретный набор категорий может быть изменён.

DR_Category используется только для боссов и элитных мобов. Для обычных мобов DR не отслеживается.

### Version

Версия эффекта, используется для invalidation и restart recovery.

5. Категории эффектов

### Buff

Положительный эффект, влияющий на цель положительно.

Примеры:

+10% Attack Speed;
+15% Spell Power;
+200 Armor;
HoT;
Shield.

### Debuff

Отрицательный эффект, влияющий на цель негативно.

Примеры:

-15% Attack Speed;
+10% damage taken;
DoT;
Stun;
Silence.

### DoT (Damage over Time)

Эффект, наносящий урон с определённым интервалом.

Пример:

Poison
Duration = 10 sec
TickInterval = 2 sec
5 ticks total

### HoT (Healing over Time)

Эффект, восстанавливающий здоровье с определённым интервалом.

Пример:

Regeneration
Duration = 12 sec
TickInterval = 3 sec
4 ticks total

### Stat Modifier

Эффект, изменяющий одну или несколько характеристик.

Использует типы модификаторов из Attributes and Stats System:

Flat;
Percent;
Multiplicative;
Conditional.

### Conditional Modifier

Модификатор, применяемый только при выполнении условия.

Примеры:

+20% Critical Chance against beasts;
+10% Dodge while below 30% HP;
+15% Spell Power during night, если будет добавлено.

### Control Effect

Эффект, ограничивающий действия цели.

Поддерживаемые типы для текущей боевой модели:

Stun — цель не может выполнять действия;
Silence — цель не может использовать способности.

Неподдерживаемые типы:

Root;
Slow;
Fear;
Charm;
Disorient, если требует пространственного поведения.

Эти эффекты не поддерживаются, так как текущая боевая модель не использует перемещение, позицию и дистанцию.

Если подобные эффекты понадобятся в будущем, они должны быть добавлены отдельным расширением.

### Shield

Эффект, поглощающий входящий урон до исчерпания.

Пример:

Magic Shield
AbsorbAmount = 500
Duration = 15 sec

### Lethal Damage Prevention

Defensive effect family, который перехватывает только **летальный** результат после расчёта shields/damage и до перехода цели в DEAD.

Примеры разрешённых правил:

```text
SetHPToPercent = 12% MaxHP
CannotReduceHPBelow = 1
OncePerCombatSession = true
```

Effect обязан иметь явные consumption/limit rules. Несколько prevention effects разрешаются deterministic priority из Effect pipeline; один lethal event не может бесконечно цеплять сам себя.

### Party Effect

Групповой эффект, применяемый к владельцу и валидным членам его Party.

```text
TargetContext = SELF_AND_PARTY_MEMBERS_IN_COMBAT
```

Party System определяет membership. Combat/Ability/Effect используют подтверждённый PartyId.

Party Effect:
- не требует distance;
- не требует position;
- не является пространственной Aura;
- не действует на случайных союзников того же encounter.

Пример:

`Боевой Клич → +AttackPower владельцу и его Party в текущем CombatSession.`

### Spatial Aura — reserved

Пространственный эффект вида «в радиусе X метров» не поддерживается, потому что текущий Combat не моделирует position/distance.

Spatial Aura можно добавить позднее только вместе с отдельными spatial rules. До этого content не должен использовать radius/proximity aura mechanics.

6. Stat Modifier Effects

Stat Modifier effects изменяют характеристики цели.

Effect System взаимодействует с Attributes and Stats System через типы модификаторов:

### Flat Modifier

+100 AttackPower
-50 Armor
+20 Strength

### Percent Modifier

+10% Attack Speed
-15% AttackSpeed
+20% Critical Damage

### Multiplicative Modifier

Damage dealt × 1.2
Damage taken × 0.85

### Conditional Modifier

Применяется только при выполнении условия, проверяемого сервером.

Effect System передаёт модификатор в Stats System, который применяет их согласно установленному порядку:

BaseValue
  ↓
+ Flat Modifiers
  ↓
+ Percent Modifiers
  ↓
× Multiplicative Modifiers
  ↓
Clamp Min/Max
  ↓
FinalValue

7. DoT / HoT

DoT и HoT являются timed effects с периодическими tick.

### Tick Mechanics

Каждый tick:

является отдельным разрешением эффекта;
использует Server Time;
может быть критическим, если это разрешено правилом эффекта;
может быть заблокирован, поглощён или уменьшен, если это определено;
может быть пропущен, если цель невалидна.

### Tick Interval

Tick Interval определяет время между tick.

Пример:

TickInterval = 2 sec
Duration = 10 sec
TickCount = 5

### Partial Ticks

Partial ticks не применяются.

Последний tick применяется только если TicksRemaining >= 1 в момент разрешения.

Пример:

Poison
TickInterval = 2 sec
Duration = 9 sec
TickCount = 4 (не 4.5)

Последний неполный интервал игнорируется.

Это упрощает имплементацию и предотвращает edge cases с дробным уроном/лечением.

Конкретное количество ticks вычисляется как:

TickCount = floor(Duration / TickInterval)

8. Snapshot vs Dynamic

Эффекты могут использовать два режима вычисления.

### Snapshot Mode

Параметры эффекта фиксируются в момент применения и не изменяются до истечения.

Пример:

12:00:00 — Poison applied, Spell Power = 500
12:00:01 — player receives +300 Spell Power buff
12:00:02 — Poison tick

Poison tick использует snapshot Spell Power = 500.

### Dynamic Mode

Параметры эффекта пересчитываются при каждом tick или разрешении.

Пример:

12:00:00 — Regen applied
12:00:03 — tick, uses current stats
12:00:06 — tick, uses current stats

### Базовое правило

По умолчанию:

DoT использует snapshot в момент применения.
HoT использует snapshot в момент применения.
Stat Modifier effects применяются динамически (статы пересчитываются при каждом изменении).
Control effects не используют snapshot.

Конкретный эффект может явно переопределить это правило, указав IsDynamic = true или IsDynamic = false.

9. Stacking Rules

Stacking определяет, как применяется повторное действие того же эффекта.

Базовые типы stacking:

### Refresh

Повторное применение обновляет ExpiresAt, не увеличивая stacks.

Пример:

Poison applied at 12:00:00, duration 10 sec
Poison applied again at 12:00:05
New ExpiresAt = 12:00:15

### Stacking

Повторное применение увеличивает stacks до MaxStacks.

Пример:

Bleed applied, stacks = 1
Bleed applied again, stacks = 2
Bleed applied again, stacks = 3 (max)

При достижении MaxStacks дальнейшее применение:

refresh duration;
или игнорируется;
или заменяет существующий эффект.

Конкретное поведение определяется per-effect.

### Independent

Каждое применение создаёт отдельную instance эффекта.

Пример:

Fireball DoT instance 1
Fireball DoT instance 2

Обе instance тикают независимо.

### Strongest Wins

Если новый эффект сильнее существующего, он заменяет его.

Если слабее — игнорируется или применяется частично.

Примеры:

+10% Attack Speed заменит +5% Attack Speed;
+5% Attack Speed не заменит +10% Attack Speed.

### Multiple Targets

Один источник может применять эффект к нескольким целям.

Каждая цель имеет отдельную instance эффекта.

10. Duration and Expiration

### Duration Rules

Duration определяется в момент применения эффекта.

ExpiresAt = AppliedAt + Duration

Если эффект refreshed:

ExpiresAt = RefreshedAt + Duration (или RemainingDuration, в зависимости от правила stacking).

### Expiration

Когда Current Server Time >= ExpiresAt:

эффект удаляется;
модификаторы эффекта удаляются из Stats System;
target освобождается от control effects;
соответствующие события эмитятся.

### Permanent Effects

Некоторые эффекты могут быть permanent (без ExpiresAt).

Примеры:

racial traits, если будут добавлены;
passive effects от талантов;
curse-подобные эффекты, требующие явного dispel.

Permanent effects имеют ExpiresAt = null и удаляются только явно.

11. Application Timing

### Instant Application

Эффект применяется немедленно в момент разрешения действия.

### Delayed Application

Эффект применяется после задержки.

Пример:

Delayed Explosion
Delay = 2 sec

### On Event Application

Эффект применяется при наступлении события.

Примеры:

on kill → +10% Attack Speed for 10 sec;
on taking damage → Shield 100;
on critical hit → apply Bleed.

### Conditional Application

Эффект применяется только при выполнении условия.

Пример:

If target below 30% HP → apply Execute debuff.

12. Tick Mechanics

### Tick Resolution

Каждый tick:

проверяет валидность цели;
проверяет active state эффекта;
применяет damage/healing/modifier;
обновляет NextTickAt;
уменьшает TicksRemaining;
эмитит EffectTick event.

### Tick Order

Если несколько эффектов тикают одновременно:

порядок tick определяется ApplicationPriority;
при равном приоритете — порядок применения (AppliedAt);
все tick в одном моменте Server Time обрабатываются атомарно в рамках aggregate.

### Tick Skip Conditions

Tick может быть пропущен если:

цель мертва;
цель неуязвима;
эффект был dispelled;
цель покинула зону действия (для aura, если будет добавлена).

13. Control Effects

Control effects ограничивают действия цели.

### Stun

Цель:

не может использовать abilities;
не может выполнять Auto Attack;
не может начинать cast.

Stun прерывает активный cast.

### Silence

Цель не может использовать abilities.

Не влияет на Auto Attack.

Silence может прерывать cast, если cast считается ability.

### Root (Not Supported)

Не поддерживается в текущей боевой модели.

Требуется перемещение и позиционирование, которые отсутствуют в игре.

### Slow (Not Supported)

Не поддерживается в текущей боевой модели.

Требуется перемещение и позиционирование, которые отсутствуют в игре.

### Fear (Not Supported)

Не поддерживается в текущей боевой модели.

Требуется перемещение и AI, которые отсутствуют в игре.

### Charm (Not Supported)

Не поддерживается в текущей боевой модели.

Требуется AI и faction system, которые отсутствуют в игре.

### Immunity

Некоторые цели могут иметь иммунитет к control effects.

Immunity определяется per-source и per-category.

Примеры:

Boss immune to Stun;
Boss immune to Silence;
Player under Freedom buff immune to Root (если будет добавлен).

Immunity проверяется до применения эффекта и до расчёта Diminishing Returns.

14. Diminishing Returns

Control effects подвержены Diminishing Returns (DR) для предотвращения бесконечного контроля.

### Scope

Diminishing Returns применяются только к:

Boss-type enemies;
Elite-type enemies.

Обычные мобы (normal enemies) не подвержены DR.

Игроки (PvP) не рассматриваются в текущей версии, так как PvP является out of scope.

### Зачем нужен DR

Без DR:

PvE: можно бесконечно станить босса или элитного моба;
Control effects становятся слишком сильными против high-value targets.

DR ограничивает эффективность повторных control effects той же категории на боссах и элитных мобах.

Для обычных мобов DR не требуется, так как они обычно убиваются быстро и не являются high-value targets.

### DR Categories

Каждый control effect имеет DR_Category.

Примеры DR категорий Базовое правило:

Stun
Silence

Отложенные DR категории (не поддерживаются в текущей модели):

Root
Slow
Disorient

Эффекты разных DR категорий не влияют друг на друга.

Пример:

Stun → DR для Stun
Silence → DR для Silence
Stun после Silence → полный duration, так как разные категории

### DR Progression

Когда босс или элитный моб получает control effect определённой DR категории:

Первое применение: 100% duration
Второе применение: 50% duration
Третье применение: 25% duration
Четвёртое и далее: иммунитет на DR_Immunity_Duration

Конкретные значения:

DR_Immunity_Duration = 15 seconds
DR_Reset_Cooldown = 30 seconds

### DR Reset

После DR_Reset_Cooldown без получения control effect той же категории:

DR state сбрасывается;
следующий control effect применяется с 100% duration.

### DR Tracking

DR state хранится на стороне mob instance (босса или элитного моба), а не персонажа.

Для каждого mob instance босса или элитного моба отслеживается:

DR_Category → текущий DR уровень
DR_Category → время последнего применения
DR_Category → время начала иммунитета

Для обычных мобов DR state не отслеживается.

Для игроков DR state не отслеживается в текущей версии (PvP out of scope).

DR state является частью mob instance и удаляется вместе с ним при смерти или despawn.

### DR и Immunity

Если босс или элитный моб имеет явный иммунитет к категории:

эффект не применяется;
DR не увеличивается;
Immunity имеет приоритет над DR.

### DR и Normal Enemies

Normal enemies не подвержены DR.

Control effects применяются к ним с полным duration каждый раз, если они не имеют явного иммунитета.

### DR и Bosses

Bosses могут иметь:

полный иммунитет к некоторым категориям;
уменьшенный DR (например, 50% вместо 100% на первом применении);
особые правила.

Конкретные правила для bosses определяются отдельно.

### DR и Elite Enemies

Elite enemies используют стандартную DR progression, если не определено иное.

Конкретные правила для elite enemies могут быть переопределены per-enemy.

15. Shield Effects

Shield поглощает входящий урон.

### Базовое поведение

Incoming damage
  ↓
Check active shields
  ↓
Reduce shield absorb amount
  ↓
If shield depleted:
    remove shield
    remaining damage applies to HP
  ↓
If shield remains:
    damage fully absorbed

### Multiple Shields

Если несколько shield активны:

порядок поглощения определяется правилом;
по умолчанию: newest shield absorbs first (последний наложенный щит расходуется первым);
конкретные правила могут переопределяться.

Newest first является более интуитивным для игроков: последний наложенный щит защищает первым.

### Shield Types

Физический shield — поглощает только физический урон.
Магический shield — поглощает только магический урон.
Universal shield — поглощает любой урон.

16. Dispel and Cleanse

Некоторые эффекты могут быть сняты dispel или cleanse.

### Dispel Categories

Базовые категории:

Magic;
Poison;
Disease;
Curse;
Physical (debuffs, которые не magic);
Enrage.

Конкретный набор категорий может быть изменён.

### Dispel Rules

Dispel ability указывает:

какие категории dispels;
сколько эффектов dispels за раз;
cooldown;
target (ally/enemy/self).

### Dispel Count

Dispel count ограничивает количество эффектов, снимаемых за одно применение.

Пример:

Cleanse
Dispel up to 2 Magic effects from target

### Undispellable Effects

Некоторые эффекты помечены как undispellable.

Примеры:

боссовые debuffs;
скриптовые эффекты;
особые mechanics.

17. Combat Interaction

### Cast Interruption

Control effects могут прерывать активный cast.

По умолчанию:

Stun прерывает cast.
Silence прерывает cast (если cast является ability).

Поведение прерывания:

прерванный cast не разрешается;
ресурс по умолчанию не возвращается (см. Resource System, правило прерывания каста);
cooldown прерванной способности определяется правилами самой способности.

### Auto Attack Interruption

Stun прерывает Auto Attack cycle.

После окончания Stun:

Auto Attack cycle возобновляется;
next attack scheduled according to Combat System rules.

### DoT in Combat

DoT tick продолжает тикать в бою.

DoT tick:

может быть критическим;
может быть заблокирован;
может быть поглощён shield;
может быть уменьшен resistance/armor, если это определено.

### Snapshot Interaction

Casted abilities, применяющие DoT/HoT:

используют snapshot параметров caster в момент начала каста;
передают snapshot в Effect System.

Instant abilities, применяющие DoT/HoT:

используют snapshot в момент применения.

18. Resource Interaction

Effect System может влиять на ресурсы через специальные эффекты.

### Resource Regeneration Modifiers

Примеры:

+50% Mana regen;
-30% Health regen;
Rage generation +5 per hit.

### Resource Burn

Примеры:

Burn 100 Mana over 5 sec;
Drain 50 Energy per second.

### Resource on Event

Примеры:

+10 Mana on kill;
+5 Rage on taking damage.

### Resource Cost Modifiers

Примеры:

-20% Mana cost of Fire spells;
Next ability costs no resource.

Все изменения ресурсов проходят через Resource System.

19. Character State Interaction

Effect System учитывает Character State при применении и действии эффектов.

### Death State

Если цель мертва:

большинство эффектов не применяются;
активные эффекты удаляются при смерти;
некоторые эффекты могут сохраняться (например, curse, corpse effect);
конкретные правила определяются per-effect.

### Respawn

При respawn:

большинство временных эффектов удаляются;
permanent effects могут сохраняться;
ресурсы устанавливаются по Resource Archetype rules;
DR state для мобов не сбрасывается (DR хранится на mob instance, не на персонаже).

### AFK Farming State

Если персонаж в AFK Farming:

combat effects не применяются;
DoT/HoT не тикают;
control effects не применяются;
zone buffs могут применяться для расчёта AFK efficiency.

### Travel State

Во время Travel:

combat effects обычно не применяются;
persistent buff могут сохраняться;
control effects не имеют смысла.

20. World Interaction

### Zone Effects

Мир может применять эффекты к персонажам в определённых зонах.

Примеры:

Safe Territory: +10% HP regen out of combat;
Dangerous Territory: -10% HP regen;
Cursed Zone: periodic damage.

### World Event Effects

Мировые события могут применять глобальные или локальные эффекты.

### Territory Modifiers

Territory Type может модифицировать эффекты:

Dangerous Territory может усиливать debuffs;
Safe Territory может уменьшать debuff duration.

21. AFK Farming Interaction

AFK Farming не использует полноценные combat effects.

По умолчанию:

combat effects не применяются во время AFK;
DoT/HoT не тикают во время AFK;
control effects не применяются во время AFK.

Однако:

passive buffs могут учитываться при расчёте AFK efficiency;
zone buffs могут учитываться при расчёте AFK rewards;
equipment effects учитываются через Farming Profile.

AFK Farming использует упрощённую модель, не требующую полного effect tick pipeline.

22. Offline Behavior

Effect System должна корректно работать для offline-персонажей.

### Persistent Effects

Эффекты, которые должны продолжать действовать offline:

продолжают тикать по Server Time;
результаты применяются при recovery или login.

### Combat Effects Offline

Если персонаж участвует в offline combat:

effects применяются согласно offline combat rules;
DoT/HoT тикают;
control effects действуют.

### AFK Offline

Если персонаж в AFK Farming:

combat effects не применяются;
AFK rewards рассчитываются по упрощённой модели.

23. Application Order

Если несколько эффектов применяются одновременно:

порядок применения определяется ApplicationPriority.

### ApplicationPriority Rules

Более высокий ApplicationPriority применяется раньше.

Default ApplicationPriority = 0 для большинства эффектов.

При равном ApplicationPriority:

порядок определяется AppliedAt (раньше применённый — раньше обработан);
при одинаковом AppliedAt — порядок определяется server-assigned sequence.

### Пример

Effect A: ApplicationPriority = 10, AppliedAt = 12:00:00.100
Effect B: ApplicationPriority = 5, AppliedAt = 12:00:00.050
Effect C: ApplicationPriority = 10, AppliedAt = 12:00:00.200

Порядок применения:

1. Effect A (priority 10, раньше)
2. Effect C (priority 10, позже)
3. Effect B (priority 5)

### Конфликт эффектов

Если эффекты конфликтуют:

более высокий приоритет может переопределить более низкий;
при равном приоритете — более поздний эффект может переопределить более ранний;
или применяются правила stacking.

24. Effect Removal

Эффекты могут быть удалены по нескольким причинам:

истечение duration;
dispel;
смерть цели;
смерть источника, если эффект source-bound;
logout, для некоторых temporary effects;
explicit removal by ability;
server-side cleanup.

### On Remove Behavior

При удалении эффекта:

модификаторы удаляются из Stats System;
triggered on-remove effects могут сработать;
соответствующие события эмитятся.

Примеры on-remove:

Explode on dispel;
Heal on expiration;
Apply secondary debuff on death.

25. Persistence

Эффекты должны быть сохраняемыми.

Для каждого активного эффекта сохраняются:

EffectId;
EffectType;
SourceId;
TargetId;
AppliedAt;
ExpiresAt;
TickInterval;
NextTickAt;
TicksRemaining;
Stacks;
ApplicationPriority;
SnapshotData;
DR_Category (только для боссов и элитных мобов, хранится на стороне mob instance);
Version.

Persistence не должен зависеть от клиента.

DR state хранится на стороне mob instance (босса или элитного моба), а не персонажа. DR state удаляется вместе с mob instance при смерти или despawn.

26. Restart Recovery

После server restart:

expired effects удаляются;
active effects пересчитываются по Server Time;
пропущенные tick могут быть:

применены catch-up;
пропущены;
объединены в aggregated tick;

конкретная catch-up policy определяется per-effect.

Базовое правило:

Timed effects use Server Time for recovery.
Expired effects are removed.
DoT/HoT catch-up is limited to prevent burst after restart.

27. Events

Effect System эмитит события:

EffectApplied
EffectRemoved
EffectTick
EffectStackChanged
EffectRefreshed
EffectDispeled
EffectExpired
DRStateChanged

События должны быть серверно-авторитетными.

### Event Payload

EffectApplied event включает:

EffectId;
EffectType;
SourceId;
TargetId;
AppliedAt;
Duration;
SnapshotData;
Stacks;
ApplicationPriority.

### Event Delivery Rules

EffectTick эмитится всегда для внутренних систем (Combat System, Resource System).

Quest System подписывается только на:

EffectApplied;
EffectExpired;
EffectDispeled;
EffectRemoved.

по умолчанию.

EffectTick доставляется Quest System только если objective явно требует tick-level tracking.

Пример objective с tick tracking:

Apply Poison 10 times
(требует EffectApplied, не EffectTick)

Пример objective с tick tracking:

Deal 500 damage with Poison ticks
(требует EffectTick)

Это предотвращает спам Quest System каждым tick каждого эффекта.

### Event Consumers

Quest System, Combat System, Analytics и другие системы могут подписываться на события.

Пример:

EffectApplied (Poison, target = Boss X)
  ↓
Quest System checks objectives
  ↓
Relevant objective progresses

28. Effect Invariants

INVARIANT-01
Сервер является источником истины для всех эффектов.

INVARIANT-02
Клиент не может напрямую применять или удалять эффекты.

INVARIANT-03
Эффект имеет определённые AppliedAt, ExpiresAt, Duration.

INVARIANT-04
Expired effects удаляются сервером.

INVARIANT-05
По умолчанию DoT/HoT используют snapshot параметров в момент применения.

INVARIANT-06
Stat Modifier effects применяются динамически при изменении характеристик.

INVARIANT-07
Stacking behavior определяется per-effect и должен быть явно указан.

INVARIANT-08
Tick разрешается по Server Time.

INVARIANT-09
Stun прерывает активный cast.

INVARIANT-10
Silence прерывает cast, если cast является ability.

INVARIANT-11
Shield поглощает incoming damage согласно правилам shield.

INVARIANT-12
Dispel применяется только к эффектам соответствующей категории.

INVARIANT-13
Effect System не хранит квестовый прогресс.

INVARIANT-14
AFK Farming по умолчанию не использует combat effects.

INVARIANT-15
После server restart expired effects удаляются.

INVARIANT-16
Effect System не определяет конкретные формулы урона или лечения.

INVARIANT-17
Effect System использует Server Time для всех временных процессов.

INVARIANT-18
Effect snapshot фиксируется в момент применения эффекта, если эффект не является dynamic.

INVARIANT-19
Partial ticks не применяются. Последний tick применяется только если TicksRemaining >= 1.

INVARIANT-20
При нескольких shields newest shield absorbs first по умолчанию.

INVARIANT-21
Control effects подвержены Diminishing Returns только для боссов и элитных мобов. Обычные мобы не подвержены DR.

INVARIANT-22
ApplicationPriority определяет порядок применения эффектов.

INVARIANT-23
EffectTick доставляется Quest System только если objective явно требует tick-level tracking.

INVARIANT-24
В текущей боевой модели поддерживаются только Stun и Silence как control effects.
Root, Slow, Fear, Charm и другие пространственные control effects не поддерживаются.

INVARIANT-25
`PARTY_EFFECT` поддерживается через Party membership и не требует distance.
Пространственная `AURA` не поддерживается до появления spatial rules.

INVARIANT-26
DR state для боссов и элитных мобов сбрасывается после DR_Reset_Cooldown без получения control effect той же категории.

INVARIANT-27
Для обычных мобов DR state не отслеживается.

INVARIANT-28
Для игроков DR state не отслеживается в текущей версии (PvP out of scope).

INVARIANT-29
DR state хранится на стороне mob instance, а не персонажа.

29. Out of Scope

Этот документ пока не определяет:

конкретные значения DoT/HoT tick damage;
конкретные длительности эффектов;
конкретные tick intervals;
конкретные stacking numbers;
конкретные dispel counts;
конкретные cooldowns dispel abilities;
конкретные shield absorb amounts;
конкретный catch-up algorithm;
AI usage of effects;
UI;
визуализацию эффектов;
конкретные категории dispel (могут быть пересмотрены);
PvP-specific effect rules;
конкретные формулы stat modifiers;
Root control effect (не поддерживается в текущей модели);
Slow control effect (не поддерживается в текущей модели);
Fear control effect (не поддерживается в текущей модели);
Charm control effect (не поддерживается в текущей модели);
Disorient control effect (не поддерживается в текущей модели);
Spatial Aura / radius effects (не поддерживаются до появления spatial rules);
boss/elite overrides базового `DR_Immunity_Duration = 15 sec`;
boss/elite overrides базового `DR_Reset_Cooldown = 30 sec`;
boss/elite overrides базовой DR progression `100% → 50% → 25% → immunity`;
конкретные правила для отдельных bosses (могут переопределять DR);
конкретные правила для отдельных elite enemies (могут переопределять DR);
Movement System;
позиционирование;
дистанция;
facing;
pathfinding;
knockback;
любые эффекты, требующие перемещения цели;
DR для обычных мобов (не применяется);
DR для игроков / PvP (не применяется в текущей версии).

---

# Source of Truth Revision v2

- Supported control effects: STUN и SILENCE.
- Slow, Root, Fear, Charm не используются текущим content.
- Добавляется `LETHAL_DAMAGE_PREVENTION` как универсальный defensive effect family.
- Party Effect не является spatial Aura и может использоваться Commander уже сейчас.
