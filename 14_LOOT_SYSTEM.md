Elyndor — Loot System Specification

Document: 14_LOOT_SYSTEM.md
System: Loot / Rewards
Status: Foundation / Source of Truth
Version: 0.1

1. Назначение

Loot System определяет получение предметных наград из игровых источников.

Система отвечает за:

Loot Table;
Loot Roll;
eligibility;
reward generation;
server RNG;
personal loot для core;
pending loot;
idempotent reward resolution;
передачу предмета Item System.

Loot System не определяет:

Item stats;
Inventory rules;
XP progression;
Quest objective logic;
Monster AI;
boss mechanics;
экономику;
торговлю.

2. Основной принцип

Loot не определяется клиентом.

Клиент не может сообщить:

«мне выпал Rare Sword»;
«я сделал ещё один loot roll»;
«этот boss drop должен быть моим».

Сервер создаёт reward result один раз для подтверждённого RewardSource.

3. Reward Source

RewardSource
  ├── SourceType
  ├── SourceId
  ├── SourceDefinitionId
  ├── LocationId
  ├── Participants
  ├── CompletedAt
  └── Metadata

SourceType core:

MONSTER_KILL
ELITE_KILL
BOSS_KILL
WORLD_EVENT
DUNGEON_ENCOUNTER
DUNGEON_COMPLETION
QUEST_REWARD
AFK_REWARD
SCRIPTED

Quest reward может использовать Loot System как общий item reward resolver, но Quest System остаётся владельцем факта завершения задания.

4. Loot Table

LootTable
  ├── LootTableId
  ├── Entries
  ├── RollGroups
  ├── Version
  └── Metadata

5. Loot Entry

LootEntry
  ├── ItemDefinitionId
  ├── WeightOrChance
  ├── MinQuantity
  ├── MaxQuantity
  ├── Conditions
  ├── UniquePerReward
  └── Metadata

6. Roll Models

core поддерживает:

CHANCE
WEIGHTED_GROUP
GUARANTEED

7. Chance Entry

Пример:

Wolf Pelt
Chance = 40%

Сервер делает независимый roll.

8. Weighted Group

Группа выбирает один результат из нескольких entries по weight.

Пример:

WeaponGroup
Sword weight 40
Dagger weight 35
Staff weight 25

Weighted Group удобен для гарантированного выбора одного предмета из набора.

9. Guaranteed Entry

Guaranteed всегда добавляется, если выполнены Conditions.

10. Server RNG

Все loot rolls выполняются сервером.

RNG context должен быть пригоден для debug/audit.

Не требуется раскрывать seed клиенту.

11. Reward Resolution Id

Каждый reward source имеет уникальный RewardResolutionId.

Повторная обработка одного RewardResolutionId возвращает уже рассчитанный LootResult.

Новый roll не выполняется.

12. Loot Result

LootResult
  ├── LootResultId
  ├── RewardResolutionId
  ├── RecipientCharacterId
  ├── Entries
  ├── State
  ├── CreatedAt
  └── ClaimedAt

State:

GENERATED
PARTIALLY_CLAIMED
CLAIMED
EXPIRED, if future rules allow
CANCELLED

Для текущей системы loot не должен expire автоматически.

13. Personal Loot

Для первой тестового окружения рекомендуется Personal Loot.

Каждый eligible персонаж получает собственный LootResult.

Преимущества:

нет Need/Greed;
не требуется Party loot master;
нет конфликтов между тестерами;
проще idempotency;
проще boss reward.

14. Eligibility

Для Monster/Boss kill core персонаж eligible если:

он является подтверждённым участником Combat;
RewardSource завершён победой;
цель действительно погибла;
персонаж не помечен как invalid/debug spectator;
один и тот же completion не был уже обработан для персонажа.

15. Contribution

Eligibility использует `ParticipationPolicy`.

Она может учитывать:
- participation time;
- qualifying combat actions;
- damage;
- effective healing;
- support effects;
- tanking/threat contribution.

Один универсальный damage threshold не используется как единственный критерий.

Сам факт присутствия в CombatSession недостаточен.

Eligibility определяется `ParticipationPolicy`, которую activity/boss передаёт Loot System вместе с подтверждённым participation context.

16. Dead Participants

Базовое правило:

персонаж, погибший во время boss fight, но участвовавший до смерти, остаётся eligible при победе.

17. Offline Participants

Если игрок disconnected, но Character продолжал участвовать в Combat:

eligibility определяется Character participation, а не connection status.

18. Class-Aware Loot

LootTable может иметь Condition:

AllowedClassId
AllowedWeaponTag
AllowedArmorTag

Рекомендуется не делать весь loot строго class-locked.

Personal Loot boss table может использовать class-aware weighted group, чтобы уменьшить бесполезные drops.

19. Quest Items

Quest item может выпадать только если:

Quest System/condition provider подтверждает eligibility;
или item drop является обычным world item.

Loot System не изменяет Quest Progress.

20. Loot and Inventory

После LootResult generation:

игрок Claim;
Loot System создаёт ItemGrant;
Item System пытается добавить item.

Если SUCCESS:
entry = claimed.

Если NO_SPACE:
entry остаётся pending.

Награда не уничтожается.

21. Automatic Loot

Для обычных мобоВ текущей системе может использовать Auto Claim.

Если Inventory имеет место:

loot автоматически выдаётся.

Если места нет:

создаётся Pending Loot.

22. Boss Loot

Boss loot всегда рекомендуется сначала создавать как persisted LootResult.

Даже если UI автоматически показывает reward screen.

Это защищает награду от disconnect/restart.

23. Pending Loot

Pending Loot хранится сервером.

Игрок может вернуться и забрать его после освобождения Inventory.

Для текущей системы не требуется mailbox.

24. Stack Merge

Item System является владельцем stack merge.

Loot System передаёт Quantity.

25. Currency

Loot System не хранит wallet balance.

Если RewardProfile содержит currency:

```text
Loot / Reward Resolver
→ CurrencyGrant
→ Currency System
```

Grant должен быть idempotent и ссылаться на RewardSource/CompletionId.

Currency System существует как `26_CURRENCY_AND_ECONOMY_SYSTEM.md`.

Loot создаёт только idempotent `CurrencyGrant`; authoritative Wallet изменяет Economy System.


26. XP

Loot System не выдаёт XP напрямую.

Reward orchestration может отдельно создать ExperienceGrant в Progression System.

27. Boss Guaranteed Rewards

Boss LootTable может содержать:

guaranteed material;
weighted equipment group;
rare independent chance.

28. Bad Luck Protection

Не входит в core.

Не добавлять pity system до получения production/test data.

29. Loot Table Version

LootTable имеет Version.

LootResult сохраняет version/context, по которому был рассчитан.

Balance patch не должен reroll уже сгенерированный loot.

30. Anti-Duplication

Критический порядок:

Confirm RewardSource
  ↓
Create/lock RewardResolutionId
  ↓
Check existing LootResult
  ↓
Generate once
  ↓
Persist LootResult
  ↓
Grant/claim items

31. Transaction Boundary

Loot generation и запись LootResult должны быть atomic.

Item Grant может быть отдельной idempotent transaction по LootEntryGrantId.

32. Restart Recovery

После restart:

GENERATED/PARTIALLY_CLAIMED LootResult остаются;
CLAIMED не выдаётся повторно;
не завершённый ItemGrant повторяется idempotently;
loot не reroll.

33. Events

Loot System эмитит:

LootGenerated
LootEntryClaimed
LootClaimCompleted
LootClaimBlockedByInventory
RareLootObtained

34. Quest Integration

Quest System может слушать ItemObtained из Item System.

Quest System не должен считать LootGenerated равным фактическому получению предмета, если objective требует владение item.

35. Analytics

Для текущей системы полезно логировать:

source;
recipient;
item;
rarity;
roll type;
loot table version;
claim state.

36. Loot Invariants

INVARIANT-01
Loot rolls выполняются сервером.

INVARIANT-02
Один RewardResolutionId не reroll.

INVARIANT-03
LootResult должен быть persisted до выдачи награды.

INVARIANT-04
Personal Loot является default для текущей системы.

INVARIANT-05
Disconnect игрока не отменяет уже earned LootResult.

INVARIANT-06
Полный Inventory не уничтожает reward.

INVARIANT-07
Item creation выполняется через Item System.

INVARIANT-08
Loot System не изменяет Level/XP.

INVARIANT-09
Loot System не хранит Quest Progress.

INVARIANT-10
Boss loot eligibility определяется server participant data.

INVARIANT-11
Умерший валидный participant может получить boss loot в core.

INVARIANT-12
LootTable patch не меняет уже generated LootResult.

37. Out of Scope

Этот документ пока не определяет:

Need/Greed;
Master Loot;
Party Loot Rules;
tradeable loot;
personal loot trading;
pity;
bad luck protection;
random affixes;
smart loot perfection;
auction;
vendor value;
currency wallet;
mail delivery;
loot boxes;
monetized loot;
PvP rewards;
seasonal reward tracks;
финальные loot tables;
UI loot animation.

---

# Source of Truth Revision v2

- Personal Loot остаётся default reward model, но система не запрещает будущие activity-specific модели.
- Loot поддерживает rarity до UNIQUE и Item Generator/Affix profiles.
- Boss/World Event eligibility использует ParticipationPolicy, а не простое присутствие в CombatSession.
- Healer/support contribution учитывается наравне с damage.
- Late join сам по себе не даёт full reward.
- Currency reward подключается через отдельную Currency/Economy System; Loot не хранит wallet самостоятельно.


## ParticipationPolicy

```text
ParticipationPolicy
├── MinimumParticipationTime
├── MinimumQualifyingActions
├── DamageContributionWeight
├── HealingContributionWeight
├── SupportContributionWeight
├── TankingContributionWeight
├── JoinCutoff, optional
└── EligibilityMode
```

Не все поля обязаны использоваться каждой activity.

Главное правило:

```text
PresenceOnly != RewardEligibility
LastHit != RewardOwnership
```

---

# Source of Truth Revision v5 — Economy / Dungeon

- Currency reward active and owned by `26_CURRENCY_AND_ECONOMY_SYSTEM`.
- Dungeon encounter/completion may create RewardSource.
- Loot never changes Wallet directly.
- Trade/Auction/Crafting do not bypass Loot/Item ownership rules.
- `BIND_ON_PICKUP` is applied by Item System when a generated reward is granted.

## Reward Activity Context

RewardSource may additionally contain:

```text
ActivityType, optional
ActivityInstanceId, optional
DungeonInstanceId, optional
DungeonEncounterId, optional
BossCompletionId, optional
DungeonCompletionId, optional
```

These fields provide audit/idempotency context and do not transfer ownership of Dungeon/Boss state to Loot.
