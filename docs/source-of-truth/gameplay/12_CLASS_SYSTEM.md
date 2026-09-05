Elyndor — Class System Specification

Document: docs/source-of-truth/gameplay/12_CLASS_SYSTEM.md
System: Classes
Status: Foundation / Source of Truth
Version: 0.1

1. Назначение

Class System определяет боевую идентичность персонажа.

Класс задаёт:

основной архетип персонажа;
Primary Attribute;
Action Resource Archetype;
базовые характеристики;
рост характеристик по уровням;
доступные категории экипировки;
доступные категории оружия;
базовый набор способностей;
role profile;
связь с Talent System.

Class System не определяет:

формулы Damage/Healing;
жизненный цикл Effect;
общие правила Ability;
общие правила Resource;
loot tables;
конкретные предметы;
квесты;
AI;
боссов;
экономику;
UI.

2. Основной принцип

Class System конфигурирует существующие системы, но не дублирует их правила.

Пример:

Class System говорит:
Warrior uses Rage.

Resource System определяет:
как Rage генерируется, тратится и decay.

Class System говорит:
Mage knows Fireball.

Ability System определяет:
как Fireball кастуется, проверяет cooldown/resource/target.

3. Class Definition

ClassDefinition
  ├── ClassId
  ├── Name
  ├── PrimaryAttribute
  ├── ResourceArchetype
  ├── RoleProfile
  ├── BaseStatProfileId
  ├── LevelGrowthProfileId
  ├── AllowedWeaponTags
  ├── AllowedArmorTags
  ├── AllowUnarmed
  ├── TalentTreeId
  ├── CompanionProfileId, optional
  ├── ClassTags
  └── Version

ClassDefinition является data-driven определением.

4. ClassId

ClassId является стабильным идентификатором.

Изменение отображаемого Name не меняет ClassId.

Другие системы используют ClassId, а не локализованное имя.

5. Primary Attribute

Каждый класс имеет один основной атакующий атрибут.

Primary Attribute используется для:

классового scaling;
экипировки;
талантов;
баланса предметов.

Primary Attribute не означает, что остальные атрибуты бесполезны.

6. Resource Archetype

Каждый класс использует один основной Action Resource.

Базовые архетипы уже определены Resource System:

Mana;
Rage;
Energy;
Focus.

Class System только выбирает ResourceArchetype.

7. Role Profile

Role Profile описывает базовое назначение класса в Combat.

Для core поддерживаются:

Tank;
Damage;
Healer;
Hybrid.

Role Profile может влиять на Threat Multipliers, если Combat System это поддерживает.

Role не является отдельным Combat State.

8. Base Stat Profile

Класс определяет стартовые базовые характеристики персонажа на Level 1.

BaseStatProfile:
Strength
Agility
Intellect
Stamina
BaseAttackPower, если требуется
BaseSpellPower, если требуется
BaseArmor, если требуется
BaseMagicResistance, если требуется
BaseCriticalChance
BaseAccuracy
BaseDodge

Конкретные текущая система значения определяются отдельным Balance Profile.

9. Level Growth Profile

Class System определяет рост основных атрибутов.

LevelGrowthProfile:
StrengthPerLevel
AgilityPerLevel
IntellectPerLevel
StaminaPerLevel
optional MaxResource growth

Progression System применяет этот профиль при Level Up.

10. Weapon Permissions

ClassDefinition содержит AllowedWeaponTags.

Примеры тегов:

DAGGER
ONE_HAND_SWORD
TWO_HAND_SWORD
AXE
MACE
STAFF
WAND
BOW

Item System является владельцем конкретных ItemDefinition.

Class System определяет только разрешение категории.

11. Armor Permissions

ClassDefinition содержит AllowedArmorTags.

Базовое правило:

LIGHT
MEDIUM
HEAVY

Это проще, чем вводить много исторических типов брони до появления достаточного количества контента.

Конкретный предмет содержит ArmorTag.

12. Equipment Requirements

Item System при Equip проверяет:

Character Class;
AllowedWeaponTags;
AllowedArmorTags;
Level requirements;
другие Item requirements.

Class System не выполняет Equip operation.


12.1. Unarmed Permission

ClassDefinition содержит:

AllowUnarmed

Если AllowUnarmed = true:

персонаж без оружия может использовать UnarmedProfile из Item/Equipment System.

Если AllowUnarmed = false:

персонаж без валидного MAIN_HAND weapon не выполняет Auto Attack.

Ability System при этом может разрешать отдельные способности без оружия, если AbilityDefinition не требует WeaponTag.

Базовое правило:

Warrior → AllowUnarmed = true
Archer → AllowUnarmed = false
Mage → AllowUnarmed = false

13. Ability Ownership

Класс определяет, какие AbilityId принадлежат его class kit.

Ability System остаётся владельцем механики способности.

Class System определяет:

StartingAbilityIds;
unlock level;
class eligibility.

14. Ability Unlock

AbilityUnlockProfile:

AbilityId
UnlockLevel
OptionalRequirement

После Level Up Class System может сообщить, какие способности стали доступны.

Для core рекомендуется автоматическое изучение class abilities при достижении уровня.

Не требуется отдельный trainer/NPC.

15. Talent Tree

Каждый класс может иметь TalentTreeId.

Class System не хранит распределённые Talent Points.

Talent System является владельцем:

выбранных талантов;
rank;
prerequisites;
respec.

16. Current Class Set

Playable roster:

```text
WARRIOR
ARCHER
MAGE
```

Future class content:

```text
PRIEST
ROGUE
```

17. Warrior

```text
ClassId: WARRIOR
PrimaryAttribute: Strength
ResourceArchetype: RAGE
RoleProfile: Tank / Damage / Hybrid Support
Companion: none
```

Armor:
- MEDIUM
- HEAVY

Weapon profiles:
- ONE_HAND_SWORD
- TWO_HAND_SWORD
- AXE
- MACE

Talent branches:
- Страж
- Берсерк
- Командир

18. Archer

```text
ClassId: ARCHER
PrimaryAttribute: Agility
ResourceArchetype: FOCUS
RoleProfile: Ranged Damage / Pet Damage / Magical Hybrid
Companion: required
```

Armor:
- LIGHT
- MEDIUM

Weapon:
- BOW

Base Archer always has one active companion.

Talent-derived Arcane profile may replace:

```text
FOCUS → MANA
Agility offensive scaling → Intellect
PHYSICAL_PET → SPIRIT_PET
```

ClassId remains `ARCHER`.

19. Mage

```text
ClassId: MAGE
PrimaryAttribute: Intellect
ResourceArchetype: MANA
RoleProfile: Magical Damage
Companion: none
```

Armor:
- LIGHT

Weapons:
- STAFF
- WAND

20. Future Classes

`PRIEST`
- Intellect;
- Mana;
- Healer / Support / Magical Damage.

`ROGUE`
- Agility;
- Energy;
- Melee Physical Damage.

Future class definitions do not alter the current playable roster.

21. Ability Progression to Level 60

Класс проектируется сразу на полный диапазон:

Level 1–60

Это не означает, что первый внутренний тест обязан содержать контент всех 60 уровней.

Рекомендуемая структура unlock:

Level 1:
2 базовые class abilities.

Levels 2–10:
ещё 2–4 способности, формирующие основной gameplay loop.

Levels 11–30:
дополнительные class tools, utility и build interactions.

Levels 31–50:
усиление identity класса и более сложные interactions.

Levels 51–60:
финальные class abilities / high-level unlocks.

Рекомендуемый размер class kit к Level 60:

8–12 активных/значимых class abilities,

не считая talent-only abilities и пассивных talent effects.

Конкретные unlock levels являются content data.

Класс не обязан получать новую способность каждый уровень.

22. Class Ability Examples

Примеры являются content direction, а не финальными числами.

Warrior:
Strike — physical instant.
Provoke — taunt.
Heavy Blow — Rage spender.
Battle Focus — temporary buff.

Archer:
Quick Shot — базовый Focus-spending Physical Shot.
Hunter Mark — target mark / precision setup.
Companion Command — команда активному спутнику.
Arcane Arrow — talent-derived Magical Arrow в ветке Тайного стрелка.

Mage:
Fireball — Casted magical damage.
Arcane Bolt — faster spell.
Burn — DoT.
Magic Shield — Shield effect.

23. Class and Damage

Class System не содержит:

if Warrior then damage formula X;
if Mage then formula Y.

Все классы используют общий Damage and Healing System.

Различия создаются через:

Stats;
Abilities;
Effects;
Resources;
Equipment;
Talents.

24. Class and Threat

Combat System является владельцем Threat.

Class System предоставляет Role Profile.

Рекомендуемые core defaults могут быть настроены через Combat balance:

Tank damage threat multiplier > 1;
Damage role default = 1;
Healer rules определяются Combat/Healing threat.

Class System не хранит ThreatTable.

25. Class and Death

Смерть не меняет ClassId.

Respawn не сбрасывает класс.

26. Class Selection

Для core класс выбирается при создании персонажа.

После подтверждения:

ClassId сохраняется.

Class change в обычном игровом процессе запрещён.

27. Character Creation

Минимальный pipeline:

Create Character
  ↓
Select Class
  ↓
Load ClassDefinition
  ↓
Apply BaseStatProfile
  ↓
Initialize Resource Archetype
  ↓
Grant Starting Abilities
  ↓
Assign starting equipment, if defined by content
  ↓
Persist Character

28. Class Validation

При загрузке персонажа сервер проверяет:

ClassId существует;
ClassDefinition version доступна;
ResourceArchetype поддерживается;
BaseStatProfile существует;
LevelGrowthProfile существует.

Invalid ClassDefinition является configuration error.

29. Balance Versioning

ClassDefinition должен иметь Version.

Balance patch может менять:

growth;
allowed items;
ability unlock levels;
role modifiers.

Изменение класса не должно требовать нового CharacterId.

30. Persistence

Character сохраняет:

ClassId

Сам ClassDefinition хранится как content data.

Не нужно копировать всё определение класса в Character row.

31. Events

Class System может эмитить:

ClassAssigned
ClassAbilityUnlocked
ClassConfigurationChanged

ClassAbilityUnlocked может использоваться:

UI;
Analytics;
Quest System, если когда-либо понадобится.

32. Class Invariants

INVARIANT-01
Каждый персонаж core имеет ровно один ClassId.

INVARIANT-02
ClassId выбирается при создании персонажа.

INVARIANT-03
Клиент не может самостоятельно изменить ClassId.

INVARIANT-04
Класс использует Resource Archetype, определённый Resource System.

INVARIANT-05
Класс не реализует собственные Damage formulas.

INVARIANT-06
Класс не реализует собственный Effect lifecycle.

INVARIANT-07
Class System предоставляет LevelGrowthProfile Progression System.

INVARIANT-08
Item System валидирует class equipment requirements.

INVARIANT-09
Ability System является владельцем выполнения abilities.

INVARIANT-10
Talent System является владельцем выбранных талантов.

INVARIANT-11
Role Profile не является Character Activity State.

INVARIANT-12
ClassDefinition является server-controlled content.

33. Out of Scope

Этот документ пока не определяет:

- дополнительные классы сверх WARRIOR / ARCHER / MAGE / planned PRIEST / ROGUE;
- Paladin и другие возможные future classes;
- dual class / multiclass;
- class change;
- hero classes;
- class trainers;
- race-class restrictions;
- PvP role balance;
- конкретный финальный numeric balance статов;
- полный ability content каждого класса;
- UI выбора класса.

Companion/Pet rules принадлежат `21_COMPANION_AND_PET_SYSTEM`, а не Class System.

---

# Source of Truth Revision v2

- Current playable roster: WARRIOR / ARCHER / MAGE.
- Future classes: PRIEST / ROGUE.
- Warrior: Strength + Rage.
- Archer: Agility + Focus; всегда имеет companion.
- Mage: Intellect + Mana.
- Arcane Archer может через talent override использовать Intellect + Mana + SPIRIT_PET без изменения ClassId.


## Authoritative Class Roster

| ClassId | Primary Attribute | Resource | Roles | Companion |
|---|---|---|---|---|
| WARRIOR | Strength | Rage | Tank / Physical DPS / Party Support | No |
| ARCHER | Agility | Focus | Ranged Physical DPS / Pet DPS / Magical Archer | Always |
| MAGE | Intellect | Mana | Magical DPS | No |
| PRIEST | Intellect | Mana | Healer / Support / Magical DPS | Future |
| ROGUE | Agility | Energy | Melee Physical DPS | Future |

`ARCHER` является официальным ClassId. `RANGER` не используется как отдельный класс.


---

# Mage Talent Tree v3 content reference

Current Mage Talent Tree:

```text
TalentTreeId: MAGE
Branches:
- FIRE / Пламя
- ARCANE / Тайная магия
- FROST / Лёд
```

Content owner: `docs/source-of-truth/gameplay/25_MAGE_TALENT_TREE.md`.

Mage remains:

```text
PrimaryAttribute = Intellect
ResourceArchetype = MANA
RoleProfile = Magical Damage
```

School tags do not create new DamageTypes; FIRE/ARCANE/FROST abilities use MAGICAL damage by default.

## Source of Truth Revision v3 — Talent-owned active abilities (2026-09-05)

- Active player abilities are never granted by ClassProfile, character creation, or character level alone.
- StartingAbilityIds and AbilityUnlocks remain serialization-compatibility fields only and must be empty in valid current content.
- TalentModifierType.AbilityModifier with UNLOCK_ABILITY is the only supported grant path for active player skills.
- CharacterDerivedState.KnownAbilityIds is derived from the active talent loadout.
- Auto Attack is a combat mechanic and is not treated as an unlocked active skill.
- Level Up may award Talent Points or satisfy a talent RequiredLevel, but does not directly grant an active ability.
- A respec or active-loadout change changes the known active ability set on the next authoritative derived-state resolution.

### Superseding rule

Sections describing StartingAbilityIds, AbilityUnlockProfile, automatic class-ability learning by level, or free Level-1 abilities are superseded by Revision v3 above. Ability definitions may still belong thematically to a class kit, but runtime access is talent-owned.
