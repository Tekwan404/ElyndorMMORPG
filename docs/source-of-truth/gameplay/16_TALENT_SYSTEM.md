Elyndor — Talent System Specification

Document: docs/source-of-truth/gameplay/16_TALENT_SYSTEM.md
System: Talents
Status: Foundation / Source of Truth
Version: 0.1

1. Назначение

Talent System определяет долгосрочную настройку class build.

Система отвечает за:

Talent Tree;
Talent Node;
Talent Rank;
Talent Points;
requirements;
prerequisites;
unlock tiers;
выбор талантов;
respec;
активацию talent effects;
связь с Stats, Abilities, Effects, Resources и Equipment.

Talent System не определяет:

общие правила Ability;
общий Effect lifecycle;
Damage formulas;
конкретный loot;
Monster AI;
Quest logic;
экономику.

2. Основной принцип

Талант не создаёт новую параллельную боевую систему.

Талант модифицирует уже существующие механики.

Пример:

+5% CriticalChance
→ Stats modifier.

Fireball applies Burn
→ Ability modification + Effect.

Critical hit restores 5 Energy
→ event-driven Resource modification.

3. Talent Tree

Каждый класс имеет один TalentTreeId.

TalentTree
  ├── TalentTreeId
  ├── ClassId
  ├── Nodes
  ├── MaxSpendablePoints
  └── Version

4. Specialization

Для core отдельная сущность Specialization не обязательна.

Стиль игры определяется вложением очков в ветки дерева.

Это сохраняет гибридные билды.

5. Branches

Рекомендуется 3 тематические ветки на класс.

Ветка является организацией дерева и не создаёт отдельный Character Class.

Текущий content:

```text
Warrior
├── Страж
├── Берсерк
└── Командир

Archer
├── Меткая стрельба
├── Повелитель зверей
└── Тайный стрелок
```

Mage branches определяются отдельным Mage Talent Tree content-документом и не придумываются Talent System заранее.

6. Talent Node

TalentNode
  ├── TalentId
  ├── TalentTreeId
  ├── BranchId
  ├── Tier
  ├── MaxRank
  ├── Prerequisites
  ├── RequiredSpentPoints
  ├── RequiredLevel, optional
  ├── RequiredWeaponTags, optional
  ├── Effects
  ├── Version
  └── Metadata

7. Talent Rank

Talent может иметь:

MaxRank = 1
или
MaxRank > 1

Для текущей системы рекомендуется большинство gameplay-changing талантов MaxRank = 1.

Небольшая часть stat talents может иметь 2–3 ranks.

8. Talent Points

Talent Points принадлежат Talent System.

Progression System предоставляет CharacterLevelChanged.

Talent System проектируется сразу под Level Cap = 60.

Базовое правило:

первое Talent Point выдаётся на Level 2;
далее +1 Talent Point за каждый новый Level.

Следовательно:

Level 1 → 0 Talent Points
Level 10 → 9 Talent Points
Level 20 → 19 Talent Points
Level 40 → 39 Talent Points
Level 60 → 59 Talent Points

Talent System после обработки CharacterLevelChanged эмитит:

TalentPointGranted

если новый уровень увеличил TotalEarnedTalentPoints.

9. Available Points

AvailableTalentPoints =
TotalEarnedTalentPoints
-
SpentTalentPoints

Значение не может быть отрицательным.

10. Talent Point Grant

Talent System не хранит вручную отдельный reward на каждый Level, если это можно вывести из current Level.

Базовое правило:

TotalEarnedTalentPoints = max(0, Level - 1)

При каждом CharacterLevelChanged Talent System сравнивает новое TotalEarnedTalentPoints с предыдущим доступным total.

Если новый уровень увеличил доступное количество очков, Talent System эмитит TalentPointGranted для каждого нового очка или одно агрегированное событие с Amount.

Это упрощает recovery и исключает потерю point при event failure: фактическое доступное количество очков всегда можно восстановить из текущего Level.

11. Tier Unlock

Talent tier может требовать:

RequiredSpentPoints in tree/branch.

Для Level Cap = 60 используется полноценная многоуровневая структура дерева.

Рекомендуемый стартовый профиль:

Tier 1 → 0 spent
Tier 2 → 5 spent
Tier 3 → 10 spent
Tier 4 → 15 spent
Tier 5 → 20 spent
Tier 6 → 25 spent
Tier 7 → 30 spent
Tier 8 → 35 spent
Tier 9 → 40 spent

Конкретные thresholds являются content data.

Ветка не обязана иметь узлы во всех tiers.

Игрок с 59 очками должен иметь возможность:

глубоко вложиться в одну ветку;
или собрать гибрид из двух/трёх веток,

если prerequisites это допускают.

12. Prerequisites

Talent может требовать:

другой TalentId;
минимальный rank другого talent;
минимальный Level;
минимум points в branch/tree;
определённый WeaponTag.

Все требования проверяются сервером.

13. Learn Talent

Pipeline:

Client selects Talent
  ↓
Server validates Character/Class
  ↓
Check points
  ↓
Check tier
  ↓
Check prerequisites
  ↓
Increase rank
  ↓
Apply/activate talent effect
  ↓
Persist
  ↓
Emit TalentLearned

14. Talent Effect Types

core поддерживает следующие типы:

STAT_MODIFIER
ABILITY_MODIFIER
EFFECT_MODIFIER
RESOURCE_MODIFIER
EVENT_TRIGGERED
EQUIPMENT_CONDITIONAL

15. Stat Modifier Talent

Пример:

+5% CriticalChance
+10 Strength
+8% AttackSpeed

Использует Talent Source в Attributes and Stats System.

16. Ability Modifier Talent

Талант может модифицировать существующий AbilityDefinition runtime profile персонажа.

Примеры:

Fireball cooldown -1 sec;
Strike resource cost -5 Rage;
Poisoned Blade applies 2 stacks;
Magic Shield absorb +20%.

Базовый AbilityDefinition не изменяется глобально.

Изменяется derived ability profile конкретного персонажа.

17. Effect Modifier Talent

Талант может менять параметры конкретного эффекта:

Duration;
MaxStacks;
TickInterval;
Snapshot mode, если явно разрешено;
AbsorbAmount scaling;
Dispel behavior, если предусмотрено.

Effect System остаётся владельцем lifecycle.

Изменение snapshot/dynamic поведения эффекта считается high-risk talent mechanic.

Talent, который меняет:

Snapshot → Dynamic
или
Dynamic → Snapshot

требует отдельного Content Review.

Такая механика может значительно изменить:

scaling;
взаимодействие с временными buffs;
DoT/HoT balance;
proc chains;
поведение после изменения Stats.

Для обычных talent nodes snapshot policy не должна меняться без явной необходимости.


18. Resource Modifier Talent

Примеры:

+10 MaxEnergy;
Rage generation from Auto Attack +2;
Mana regeneration +10%;
ability refunds 20% cost on crit.

Resource System применяет фактическое изменение.

19. Event-Triggered Talent

Талант может реагировать на подтверждённое серверное событие.

Примеры:

OnCriticalHit
OnEnemyKilled
OnEffectApplied
OnDamageTaken
OnCastCompleted

Результат может:

применить Effect;
изменить Resource;
сбросить Cooldown;
дать temporary Stat modifier.

20. Proc Safety

Event-triggered talents должны предотвращать бесконечные proc loops.

Каждый trigger должен иметь:

AllowedSourceTypes;
CanTriggerFromProc;
InternalCooldown, optional;
MaxTriggersPerEventChain, optional.

По умолчанию:

proc-created event не запускает тот же талант рекурсивно.

21. Equipment Conditional Talent

Talent может быть активен только при нужной экипировке.

Пример:

while using DAGGER → +5% CriticalChance.

Если weapon снимается:

talent остаётся изученным;
его effect становится inactive.

22. Talent and Item Requirements

Talent System читает Equipment State.

Item System не хранит talent state.

23. Talent and Class

Персонаж может изучать только Talent Tree своего ClassId.

Class change отсутствует в core.

24. Talent and Progression

Level определяет:

число earned points;
optional RequiredLevel;
unlock thresholds.

Talent System не изменяет Level.

25. Talent and Ability Unlock

Для текущей системы талант может:

модифицировать способность;
или открыть новую активную способность.

Если талант открывает AbilityId:

Talent System добавляет derived KnownAbility source = TALENT.

При respec эта способность удаляется, если нет другого источника.

26. Multiple Ability Sources

Known Ability может происходить из:

CLASS
TALENT
SCRIPTED

Удаление одного source не удаляет способность, если существует другой active source.

27. Respec

Respec изменяет распределение очков внутри выбранного Talent Loadout.

Respec разрешён только:
- вне Combat;
- не во время Cast;
- не во время опасного transition state;
- в Safe Territory или через разрешённый сервис/NPC.

Первый полный reset персонажа может быть бесплатным. Дальнейшая стоимость относится к Economy content и не должна hardcode'иться Talent System.

28. Talent Loadouts

Каждый персонаж имеет **ровно два сохранённых Talent Loadout**:

```text
LOADOUT_1
LOADOUT_2
```

Один и только один является активным.

Каждый loadout хранит собственные:
- spent points;
- talent ranks;
- derived ability/talent configuration.

Переключение:
- разрешено только вне Combat;
- не требует повторного распределения очков;
- атомарно снимает talent-derived effects/abilities старого loadout;
- валидирует новый loadout;
- применяет effects/abilities нового loadout;
- не меняет ClassId;
- не сбрасывает cooldown уже использованных AbilityId;
- Ability cooldown state хранится Ability System независимо от того, активен ли сейчас talent source этой способности;
- не очищает companion recovery/death state;
- не может использоваться для восстановления ресурса.

Если loadout меняет Action Resource archetype (например Archer `Focus ↔ Mana`), сохраняется **процент заполнения ресурса**:

```text
newCurrent = round(newMax * oldCurrent / oldMax)
```

после чего применяется clamp Resource System.

Это ровно две сохранённые сборки, а не две одновременно активные специализации. Третьего saved loadout нет.

29. Switching Restrictions

Switch запрещён:
- IN_COMBAT;
- DEAD;
- во время Cast;
- во время перехода между локациями;
- если новый loadout не проходит validation после content patch.

30. Loadout Persistence

Полная persistence-схема определена в разделе 31. Loadout switch всегда сохраняет ActiveLoadoutId и оба TalentLoadout атомарно.

31. Talent State

```text
CharacterTalentState
├── CharacterId
├── TalentTreeId
├── ActiveLoadoutId
├── Loadout1
├── Loadout2
├── AvailablePoints
├── TalentVersion
├── StateVersion
└── LastChangedAt

TalentLoadout
├── LoadoutId
├── SelectedRanks
├── SpentPointsByBranch
├── ValidationVersion
└── LastChangedAt
```

`SelectedRanks` хранится отдельно для каждого из двух loadout. Derived modifiers/abilities пересобираются из TalentDefinition и не являются единственным persistence source.

32. Validation After Content Patch

Если TalentDefinition изменился и старый build стал невалидным:

сервер должен выполнить TalentValidation.

Рекомендуемая policy:

invalid node reset;
affected points returned;
player notified.

Нельзя оставлять невозможный hidden build.

33. Persistence

Talent selection сохраняется.

Derived modifiers не являются единственным source of truth.

После restart:

Talent State загружается;
definitions загружаются;
talents валидируются;
derived effects пересобираются;
Stats invalidated.

34. Events

Talent System эмитит:

TalentPointGranted
TalentPointAvailabilityChanged
TalentLearned
TalentRankChanged
TalentRespecCompleted
TalentEffectActivated
TalentEffectDeactivated
TalentBuildInvalidated

35. Quest Integration

Quest System может при необходимости слушать TalentLearned.

В базовой системе квесты не должны требовать конкретный билд.

36. Talent Scope for Level 60

Talent System проектируется сразу под Level Cap = 60.

Authoritative Level 60 target:

- 3 branches per class;
- примерно **28–35 meaningful nodes на ветку**;
- ориентир **85–100 nodes на класс**;
- 59 earned Talent Points на Level 60;
- каждая ветка должна содержать примерно **68–72 возможных rank-points**, чтобы игрок не мог забрать почти всё;
- gameplay-changing nodes обычно MaxRank 1–2;
- scaling/support nodes могут иметь 2–5 ranks, если каждый rank имеет понятную ценность.

Цель:

дать глубину полноценным Level 60 builds;
сохранить гибридные распределения;
не заполнять деревья десятками обязательных пустых +stat узлов.

Реализация может включать content итерациями, но Talent System и дерево сразу проектируются на полный диапазон Level 1–60.

37. Talent Design Rules

Базовое правило:

не более ~20–30% талантов — чистые +stat;
остальные должны менять gameplay, resource, ability или effect.

Каждая ветка должна иметь понятную fantasy.

Не делать обязательный «правильный» путь из узлов, без которых класс не работает.

Базовый класс должен быть играбелен без talent points.

38. Talent Invariants

INVARIANT-01
Talent selection является server-authoritative.

INVARIANT-02
Персонаж может изучать только дерево своего класса.

INVARIANT-03
SpentTalentPoints не могут превышать earned points.

INVARIANT-04
Prerequisites проверяются сервером.

INVARIANT-05
Talent System не реализует собственный Damage formula.

INVARIANT-06
Stat talent применяется через Stats System.

INVARIANT-07
Effect talent применяется через Effect System.

INVARIANT-08
Resource talent применяется через Resource System.

INVARIANT-09
Ability talent применяется через derived Ability profile.

INVARIANT-10
Talent proc не должен бесконечно запускать сам себя.

INVARIANT-11
Respec запрещён в Combat.

INVARIANT-12
Respec должен полностью удалить старые talent-derived effects.

INVARIANT-13
После restart build восстанавливается из Talent State.

39. Out of Scope

Этот документ пока не определяет:

dual talent pages;
VIP unlocks;
paid respec;
PvP-only talents;
account-wide talents;
paragon trees;
infinite progression;
talent items;
talent trading;
temporary seasonal talents;
class change migration;
полные финальные trees;
финальный баланс каждого node;
UI talent tree.

---

# Source of Truth Revision v2

- Level 60 / 59 points / 3 branches / tier every 5 spent / capstone at 40 spent + 1 point.
- Exactly 2 saved Talent Loadouts; exactly one active.
- Switching loadout is allowed only outside Combat and applies atomically.
- Talent tags may target PHYSICAL_PET, SPIRIT_PET, WeaponTag, AbilityTag.
- Warrior and Archer final talent trees are authoritative class content documents.


---

# Current class talent content documents

```text
docs/source-of-truth/gameplay/22_WARRIOR_TALENT_TREE.md
docs/source-of-truth/gameplay/23_ARCHER_TALENT_TREE.md
docs/source-of-truth/gameplay/25_MAGE_TALENT_TREE.md
```

All three use:
- Level Cap 60;
- 59 earned points;
- 3 branches;
- tier unlock every 5 spent;
- capstone at 40 spent + 1 point;
- exactly 2 saved Talent Loadouts.
