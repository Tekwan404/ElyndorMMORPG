Elyndor — AFK Farming System Specification

Document: docs/source-of-truth/gameplay/03_AFK_FARMING_SYSTEM.md
System: AFK Farming
Status: Foundation / Source of Truth
Version: 0.3

1. Назначение

AFK Farming позволяет игроку явно отправить персонажа в пассивный фоновый режим получения небольшого бонуса в разрешённой локации.

AFK Farming является отдельной системой и не заменяет Combat System, World System или активный игровой процесс.

2. Основной принцип

AFK Farming является passive bonus mode.

AFK Farming не предназначен для полноценной симуляции боя.

AFK Farming не должен заменять активную игру.

AFK Farming задумывается как маленький бонус за то, что персонаж остаётся в подходящей локации.

Базовая модель:

Player explicitly starts AFK Farming
  ↓
Character remains in allowed location
  ↓
Server Time continues
  ↓
Limited bonus result accrues
  ↓
Player returns
  ↓
Bonus result is granted or reported

3. Разрешённые территории

AFK Farming разрешён только в:

Safe Territory;
Adventure Territory.

При этом конкретная локация должна явно разрешать AFK Farming.

Базовое правило:

AFK Farming allowed only if:
  Territory Type is Safe or Adventure
  AND Location.allowAfkBonus = true

В Dangerous Territory AFK Farming по умолчанию запрещён.

В Extreme / Endgame Territory AFK Farming по умолчанию запрещён.

В Instanced Territory AFK Farming по умолчанию запрещён, если инстанс явно не разрешает другой режим.

4. Запуск AFK Farming

Игрок должен явно запустить AFK Farming.

Он выбирает:

локацию;
режим фарма, если он поддерживается локацией;
продолжительность, если система ограничивает длительность.

После подтверждения персонаж переходит в состояние:

AFK_FARMING

Это состояние отличается от обычного нахождения в локации.

5. Ordinary Presence vs AFK Farming

Обычное нахождение персонажа в локации не является AFK Farming.

Ordinary Presence

Character in Location
  ↓
Wait / Explore
  ↓
Possible Encounter
  ↓
Combat
  ↓
Wait again

AFK Farming

Start AFK Farming
  ↓
Passive bonus mode
  ↓
Limited bonus results
  ↓
No full combat simulation

Обычный logout в лесу не является командой «начать фарм».

6. AFK Bonus Profile

Для определения эффективности AFK Farming система может использовать упрощённый профиль эффективности.

Он может основываться на:

character level;
build;
equipment;
abilities;
локации;
типе противников;
реальной боевой статистике, если она доступна.

Однако в базовой модели профиль влияет только на скорость или размер бонуса.

Профиль не определяет:

смерть;
расход ресурсов;
поломку экипировки;
необходимость использовать consumables.

Если профиль отсутствует, устарел или недостоверен, система может:

запретить AFK Farming;
использовать минимальную базовую ставку;
потребовать повторной валидации.

Конкретная техническая модель профиля будет определена позднее.

7. Награды

AFK Farming даёт ограниченный бонусный результат.

Награда может включать:

опыт;
валюту;
ресурсы;
простые предметы;
другие результаты, разрешённые локацией.

Базовые правила:

AFK Farming не должен давать больше, чем сопоставимый активный игровой процесс.
AFK Farming не должен быть основным способом фарма.
AFK Farming должен иметь ограничения.

Ограничения могут включать:

max AFK duration;
daily AFK cap;
diminishing returns;
inventory capacity;
location reward cap;
reward rate reduction after long sessions.

Конкретные значения и формулы будут определены позднее.

8. Отсутствие расхода ресурсов

AFK Farming по умолчанию не расходует ресурсы.

В AFK Farming не расходуются:

mana;
energy;
rage;
focus;
potion charges;
ammo;
durability;
consumables;
другие боевые ресурсы.

Если будущая система захочет ввести продвинутый режим AFK с расходом ресурсов, этот режим должен быть отдельным и явно отличаться от базового AFK Farming.

9. Отсутствие смерти и негативных последствий

AFK Farming по умолчанию не наносит урон персонажу.

AFK Farming по умолчанию:

не вызывает смерть;
не накладывает respawn;
не ломает экипировку;
не создаёт injury/debuff;
не перемещает персонажа в город.

AFK Farming является безопасным фоновым бонусом в разрешённых локациях.

Если в будущем будет добавлен risk-AFK режим, он должен быть отдельной системой с отдельными правилами.

10. Взаимодействие с World и Combat

AFK Farming не является полноценным присутствием персонажа в мире для целей ordinary encounter generation.

Пока персонаж находится в AFK Farming:

обычные world encounters не начинаются;
Combat Session по умолчанию не создаётся;
враги не спавнятся как реальные боевые сущности;
offline encounter director не атакует персонажа.

AFK Farming не должен конкурировать с обычным offline presence.

Если происходит scripted world event, явно прерывающий AFK, AFK Farming должен быть остановлен.

Если персонаж по какой-то причине переходит в реальный бой, AFK Farming должен быть остановлен.

11. AFK Farming Duration

Время AFK Farming определяется Server Time.

Пример:

Start = 12:00
End = 14:00
Elapsed Time = 2 hours

Не требуется обрабатывать каждую секунду отдельно.

AFK Farming должен корректно работать после server restart.

12. Offline Operation

AFK Farming предназначен для работы без постоянного присутствия игрока.

Start AFK Farming
  ↓
Close application
  ↓
Server Time continues
  ↓
Player returns
  ↓
AFK result calculated

Offline не прерывает AFK Farming автоматически.

13. Перемещение

AFK Farming не перемещает персонажа между локациями.

Пока AFK Farming активен, персонаж остаётся в той локации, где был запущен AFK Farming.

Если игрок хочет переместиться:

Stop AFK Farming
  ↓
Travel
  ↓
Start AFK Farming in new location, if allowed

Начало travel должно останавливать AFK Farming.

14. Ограничения

AFK Farming может иметь ограничения.

Возможные ограничения:

доступность локации;
Territory Type;
Location.allowAfkBonus;
уровень персонажа;
максимальная продолжительность AFK;
daily cap;
weekly cap;
diminishing returns;
вместимость инвентаря;
состояние персонажа;
нахождение в бою;
нахождение в instance;
нахождение в travel state;
нахождение в Dangerous/Extreme territory.

Конкретные значения пока не фиксируются.

15. Inventory

Если инвентарь персонажа заполняется, AFK Farming должен реагировать по правилам системы.

Базовый рекомендуемый вариант:

Если инвентарь полон:
  AFK Farming stops
  OR reward accrual stops
  OR reward is capped according to economy rules

Конкретное поведение будет определено позднее.

16. AFK Farming и Quest Progress

AFK Farming не должен автоматически продвигать любой активный квест только потому, что система рассчитала бонусные результаты.

По умолчанию AFK-прогресс для квестовой цели запрещён.

Конкретная цель Quest System может явно разрешать учитывать результат AFK Farming.

Пример разрешённой цели:

Collect 50 Wolf Pelts
AFK Progress Allowed

Пример цели, для которой AFK-прогресс не должен использоваться:

Defeat the Bandit Leader personally
AFK Progress Not Allowed

AFK Farming сообщает Quest System фактический рассчитанный результат, а Quest System самостоятельно решает, может ли этот результат изменить прогресс конкретной цели.

17. Events

AFK Farming может предоставлять внешним системам события.

Примеры:

AfkBonusStarted
AfkBonusStopped
AfkBonusCompleted
AfkBonusRewardCalculated
AfkBonusInterrupted

Эти события должны явно указывать источник:

AFK_BONUS

AFK Farming по умолчанию не должен эмитить обычный EnemyKilled для каждого расчётного убийства, если это не требуется отдельной системой и не согласовано с Quest/Economy правилами.

18. AFK Farming Invariants

INVARIANT-01
AFK Farming является отдельной системой.

INVARIANT-02
AFK Farming является пассивным бонусным режимом.

INVARIANT-03
AFK Farming не является полноценной боевой симуляцией.

INVARIANT-04
Игрок должен явно запустить AFK Farming.

INVARIANT-05
Обычное нахождение персонажа в локации не считается AFK Farming.

INVARIANT-06
AFK Farming разрешён только в Safe Territory и Adventure Territory по умолчанию.

INVARIANT-07
AFK Farming требует, чтобы конкретная локация разрешала AFK.

INVARIANT-08
AFK Farming запрещён в Dangerous Territory по умолчанию.

INVARIANT-09
AFK Farming запрещён в Extreme / Endgame Territory по умолчанию.

INVARIANT-10
AFK Farming не расходует ресурсы по умолчанию.

INVARIANT-11
AFK Farming не вызывает смерть по умолчанию.

INVARIANT-12
AFK Farming не ломает экипировку по умолчанию.

INVARIANT-13
AFK Farming не запускает обычные world encounters по умолчанию.

INVARIANT-14
AFK Farming не создаёт реальный Combat Session по умолчанию.

INVARIANT-15
AFK Farming не перемещает персонажа между локациями.

INVARIANT-16
AFK Farming использует Server Time.

INVARIANT-17
AFK Farming не продвигает квестовые цели, если Quest System явно не разрешает AFK-прогресс для этой цели.

INVARIANT-18
AFK Farming должен иметь ограничения, чтобы не заменять активный игровой процесс.

19. Out of Scope

Этот документ пока не определяет:

точную математическую модель бонуса;
конкретные reward rates;
конкретные daily caps;
конкретные diminishing returns;
конкретные формулы опыта;
конкретные loot tables;
экономику;
UI;
конкретную реализацию сервера;
конкретную структуру хранения AFK session;
будущий risk-AFK режим;
конкретные правила inventory overflow.

---

# Source of Truth Revision v2

- AFK Farming остаётся пассивным bonus mode, а не скрытой offline-симуляцией полноценного боя.
- AFK прогресс вводится и балансируется как полноценная игровая система по схеме `реализация → тест → баланс`, а не как отдельная урезанная версия правил.
- AFK не даёт автоматически quest progress, boss eligibility или уникальный endgame loot, если профиль награды явно этого не разрешает.

# Source of Truth Revision v5 — Economy

AFK Reward Profile may contain Gold through `CurrencyGrant`.

Rules:
- Economy System owns Wallet.
- AFK does not mint Crystal as an ordinary repeatable reward.
- AFK Gold/hour must remain below intended active-play Gold/hour.
- `AfkSessionId` is part of currency reward idempotency.
