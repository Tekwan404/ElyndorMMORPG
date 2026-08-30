Elyndor — Progression System Specification

Document: docs/source-of-truth/gameplay/11_PROGRESSION_SYSTEM.md
System: Progression
Status: Foundation / Source of Truth
Version: 0.1

1. Назначение

Progression System определяет долгосрочное развитие персонажа через Level и Experience.

Система отвечает за:

получение Experience;
валидацию источника Experience;
накопление Experience;
переход между уровнями;
Level Cap;
обработку нескольких Level Up подряд;
уведомление других систем о новом уровне;
применение level-based growth;
сохранение и восстановление Progression State.

Progression System не определяет:

конкретные классы;
таланты;
предметы;
лут;
квесты;
боевые формулы;
характеристики конкретного класса;
способности;
экономику;
UI.

2. Основной принцип

Progression является серверно-авторитетной.

Клиент не может заявить:

«я получил 500 XP»;
«мой уровень теперь 10»;
«я выполнил условие повышения уровня».

Клиент только отображает подтверждённое состояние.

Experience поступает только от доверенных серверных систем.

3. Progression State

Для каждого персонажа сохраняется:

ProgressionState
  ├── CharacterId
  ├── Level
  ├── CurrentXP
  ├── LifetimeXP
  ├── LevelCapProfileId
  ├── ProgressionVersion
  └── LastProgressionChangeAt

Level — текущий уровень персонажа.

CurrentXP — прогресс внутри текущего уровня.

LifetimeXP — суммарно подтверждённый опыт, полученный персонажем. Используется для analytics/audit и не является источником Level.

4. Level

Level является целым положительным значением.

Минимальный уровень:

Level = 1

Персонаж не может иметь Level < 1.

Базовое правило:

Level Cap = 60

Level Cap является конфигурацией контента, а не жёстким ограничением движка.

5. Experience

Experience — серверно подтверждённая величина прогресса.

Experience может поступать от:

Combat Result;
Quest Reward;
AFK Farming Reward;
Boss / World Event Reward;
scripted progression grant;
admin/debug grant в тестовой среде.

Каждый источник должен указывать SourceType и SourceId.

6. Experience Grant

Запрос на выдачу опыта:

ExperienceGrant
  ├── GrantId
  ├── CharacterId
  ├── Amount
  ├── SourceType
  ├── SourceId
  ├── CreatedAt
  └── Metadata

GrantId должен быть уникальным.

Повторная обработка одного GrantId не должна повторно начислять Experience.

7. Idempotency

Experience Grant является idempotent operation.

Модель:

Receive ExperienceGrant
  ↓
Check GrantId
  ↓
Already processed?
  ├── yes → return previous result
  └── no  → apply XP
              ↓
           persist grant result

Это защищает от:

повторной доставки событий;
повторного запроса после network timeout;
server retry;
duplicate boss/quest events.

8. XP Curve

Progression System использует XP Curve Profile.

XP Curve определяет, сколько XP требуется для перехода:

Level N → Level N + 1

Конкретные значения должны быть data-driven.

Для Level Cap = 60 рекомендуется не использовать одну жёсткую математическую формулу на все уровни.

Вместо этого используется XP Curve Table:

XPToNextLevel[Level]

Причина:

ранние уровни должны проходиться быстро;
середина прогрессии должна постепенно замедляться;
уровни 50–60 не должны превращаться в математическую стену;
баланс отдельных диапазонов должен меняться без изменения Progression System.

Стартовый принцип кривой:

Levels 1–10:
быстрый onboarding и частые Level Up.

Levels 11–30:
умеренное линейно-нарастающее требование.

Levels 31–50:
заметное, но контролируемое замедление.

Levels 51–60:
длиннее предыдущих уровней, но без экспоненциального/квадратичного скачка.

До создания content Balance конкретные 59 значений XPToNextLevel являются content data.

Для технических тестов допустим временный fallback:

XPToNextLevel(Level) = 100 + (Level - 1) × 150

но этот fallback не является финальной кривой для Level Cap = 60.

9. Level Up Resolution

При добавлении Experience:

CurrentXP += GrantedXP

Пока:

CurrentXP >= XPToNextLevel(Level)
AND Level < LevelCap

выполняется:

CurrentXP -= XPToNextLevel(Level)
Level += 1
Apply Level Growth
Emit LevelUp

Таким образом один большой ExperienceGrant может повысить персонажа сразу на несколько уровней.

10. Level Cap

Если персонаж достиг Level Cap:

Level временно не увеличивается;
Experience продолжает начисляться;
CurrentXP продолжает накапливаться;
LifetimeXP продолжает учитывать все подтверждённые Experience Grants.

На Level Cap CurrentXP не сбрасывается и не ограничивается значением XPToNextLevel текущего уровня.

Пример:

Level Cap = 60
Character Level = 20
CurrentXP = 0

Игрок получает ещё 5000 XP.

Результат:

Level = 20
CurrentXP = 5000

Если позднее Level Cap повышается, накопленный CurrentXP автоматически проходит через обычный Level Up Resolution.

Пример:

Level Cap повышен с 20 до 25
Character Level = 20
CurrentXP = 5000

Server:
  ↓
checks XPToNextLevel(20)
  ↓
performs Level Up while XP is sufficient
  ↓
stops when CurrentXP is insufficient or new Level Cap is reached

Таким образом персонаж может заранее накопить XP на максимальном уровне и после повышения Level Cap автоматически получить один или несколько уровней.

Это ожидаемое поведение системы.

Level Cap ограничивает Level, но не блокирует получение и накопление Experience.

11. Level Growth

Progression System отвечает за факт применения роста уровня, но не придумывает классовые значения.

Class System предоставляет LevelGrowthProfile.

Пример:

LevelGrowthProfile
  ├── StrengthPerLevel
  ├── AgilityPerLevel
  ├── IntellectPerLevel
  ├── StaminaPerLevel
  ├── BaseHPGrowth
  └── ResourceGrowth, optional

Progression System применяет профиль класса при Level Up.

Attributes and Stats System после этого пересчитывает итоговые характеристики.

12. Порядок Level Up

Рекомендуемый pipeline:

Experience applied
  ↓
Level threshold reached
  ↓
Level increment
  ↓
Class LevelGrowthProfile applied
  ↓
Attributes/Stats invalidated
  ↓
Resource maximums recalculated
  ↓
Talent System notified
  ↓
Quest System notified
  ↓
LevelUp event emitted to client

13. Level Up и CurrentHP

Для текущей системы Level Up является моментом полного восстановления персонажа.

После завершения пересчёта MaxHP:

CurrentHP = MaxHP

Полное восстановление происходит после применения Level Growth и пересчёта итогового MaxHP.

Если один ExperienceGrant вызывает несколько Level Up подряд, полное восстановление выполняется после завершения всей цепочки Level Up.

14. Level Up и Action Resource

Для текущей системы Level Up также полностью восстанавливает основной Action Resource.

После завершения пересчёта MaxResource:

CurrentResource = MaxResource

Для Rage Archetype используется исключение:

CurrentResource = 0

поскольку Rage по своей базовой модели начинается с 0 и генерируется боевыми действиями.

Если конкретный Resource Archetype позднее определит особое поведение Level Up recovery, его правило имеет приоритет.

15. Class Change

Progression System не определяет смену класса.

Если Class Change когда-либо будет добавлен:

Progression Level сохраняется;
перерасчёт class growth выполняется отдельной миграционной операцией.

Class Change не входит в core.

16. Death

Смерть:

не уменьшает Level;
не уменьшает CurrentXP;
не уменьшает LifetimeXP.

Это согласуется с Character System.

17. Combat Experience

Combat System не начисляет Experience напрямую.

После подтверждённого Combat Result внешняя reward-логика может создать ExperienceGrant.

Минимальный серверный контекст:

EnemyId;
EnemyType;
EnemyLevel;
Participants;
CombatResult;
LocationId.

Формула XP за конкретного монстра является content/balance rule.

18. Experience Eligibility

Experience выдаётся только подтверждённым eligible participant.

Базовые условия:
- противник действительно погиб;
- Combat Result подтверждён сервером;
- один death result не награждается повторно;
- Last Hit не определяет ownership Experience.

Для группового PvE используется Party + ParticipationPolicy.

Eligible Party member:
- состоит в Party;
- входит в activity/Combat participation context;
- выполняет минимальные условия участия.

Формула распределения XP является data-driven balance profile.

Она может использовать:
- full personal base XP;
- group multiplier;
- party bonus;
- anti-leech threshold.

Но никогда не использует Last Hit как главный критерий.

19. AFK Experience

AFK Farming может выдавать Experience только через подтверждённый AfkBonusRewardCalculated.

AFK Progression:

не симулирует отдельные EnemyKilled;
не обходит AFK caps;
использует отдельный SourceType = AFK_BONUS.

20. Boss / World Event Experience

Boss System или Reward pipeline может создать ExperienceGrant после подтверждённого завершения события.

Один BossCompletionId не должен давать Experience повторно.

21. Quest Experience

Quest System при выдаче награды может создать ExperienceGrant.

Quest System не изменяет Level самостоятельно.

22. Talent Integration

Progression System не хранит Talent Tree и не выдаёт Talent Points напрямую.

После Level Up система эмитит:

CharacterLevelChanged

Talent System получает CharacterLevelChanged и самостоятельно определяет:

получено ли новое Talent Point;
открылась ли новая tier;
доступны ли новые узлы.

Если новый уровень предоставляет Talent Point, Talent System после обработки CharacterLevelChanged эмитит:

TalentPointGranted

Progression System не является владельцем TalentPointGranted.

23. Class Integration

Class System предоставляет Progression System:

ClassId;
LevelGrowthProfileId;
optional class-specific Level requirements.

Progression System не содержит hardcoded switch по классам.

24. Item Integration

Item System может использовать Level как requirement.

Progression System не проверяет возможность экипировки при каждом Level Up.

Так как Level не понижается в core, надетые предметы не становятся невалидными из-за Progression.

25. Quest Integration

Progression System эмитит:

CharacterLevelChanged;
CharacterReachedLevel.

Quest System подписывается на события и самостоятельно проверяет objectives.

Progression System не хранит Quest Progress.

26. Persistence

Persisted Progression State:

CharacterId;
Level;
CurrentXP;
LifetimeXP;
LevelCapProfileId;
ProgressionVersion;
processed Experience Grant identifiers или эквивалентная idempotency запись.

Изменение XP и Level должно сохраняться атомарно.

27. Transaction Boundary

Операция:

Apply Experience
+
Resolve Level Ups
+
Persist Progression
+
Register processed GrantId

должна быть одной логической транзакцией.

Нельзя сохранить XP, но не сохранить Level Up.

28. Restart Recovery

После server restart:

Level загружается из persisted state;
CurrentXP загружается;
Level Cap проверяется;
повторная доставка уже обработанного ExperienceGrant не начисляет XP повторно;
Stats пересчитываются при необходимости.

29. Events

Progression System эмитит:

ExperienceGranted
LevelUpStarted
CharacterLevelChanged
CharacterReachedLevel
LevelCapReached

ExperienceGranted включает:

CharacterId;
GrantId;
Amount;
SourceType;
SourceId;
LevelBefore;
LevelAfter;
CurrentXPAfter.

30. Progression Profile

Базовое правило:

Start Level = 1
Level Cap = 60
No XP loss on death
No rested XP
No prestige
No rebirth
No alternate advancement
No account-wide progression

Цель текущей системы Progression:

дать игроку быстрые ранние Level Up;
показать рост силы и развитие build;
открывать способности, таланты и экипировку на протяжении всей кривой 1–60;
не требовать прохождения всех 60 уровней для первого внутреннего теста;
позволить content постепенно заполнять диапазоны уровней без изменения фундаментальной системы.

31. Progression Invariants

INVARIANT-01
Level >= 1.

INVARIANT-02
Level не может превышать текущий Level Cap, но Experience и CurrentXP могут продолжать накапливаться на Level Cap.

INVARIANT-03
Клиент не может изменять XP или Level.

INVARIANT-04
Experience поступает только через серверно подтверждённый ExperienceGrant.

INVARIANT-05
ExperienceGrant должен быть idempotent.

INVARIANT-06
Один GrantId не может быть применён дважды.

INVARIANT-07
Level Up выполняется сервером.

INVARIANT-08
Один ExperienceGrant может вызвать несколько Level Up.

INVARIANT-09
Смерть не уменьшает Level или XP.

INVARIANT-10
Level Growth определяется Class System, но применяется через Progression pipeline.

INVARIANT-11
Изменение Level инвалидирует итоговые Stats.

INVARIANT-12
Progression System не хранит Talent Tree.

INVARIANT-13
Progression System не хранит Quest Progress.

INVARIANT-14
Progression System не создаёт Items.

INVARIANT-15
Level/XP сохраняются независимо от подключения клиента.

INVARIANT-16
При повышении Level Cap накопленный CurrentXP должен быть обработан обычным Level Up Resolution.

INVARIANT-17
Level Up В текущей системе полностью восстанавливает CurrentHP после завершения всей цепочки Level Up.

INVARIANT-18
Level Up В текущей системе восстанавливает Action Resource согласно Resource Archetype; Rage возвращается к 0.

INVARIANT-19
Group Experience использует Party + ParticipationPolicy; membership без участия не гарантирует XP.

INVARIANT-20
Last Hit не определяет право на Experience.

32. Out of Scope

Этот документ не определяет:

финальный XP curve;
конкретный XP каждого моба;
конкретный XP каждого квеста;
rested XP;
mentor system;
prestige;
rebirth;
account level;
battle pass;
PvP progression;
seasonal progression;
level scaling мира;
автоматическое масштабирование монстров;
конкретный UI.

---

# Source of Truth Revision v2

- Level Cap = 60 с первого полноценного content profile.
- Talent Points: первый point на Level 2, всего 59 к Level 60.
- Group XP не зависит от Last Hit.
- Eligible Party members в том же CombatSession получают XP через ParticipationPolicy; случайный не-Party participant получает reward только если activity profile это допускает.
- Level Up полностью восстанавливает HP; Mana/Energy/Focus восстанавливаются до max, Rage сбрасывается в 0.
