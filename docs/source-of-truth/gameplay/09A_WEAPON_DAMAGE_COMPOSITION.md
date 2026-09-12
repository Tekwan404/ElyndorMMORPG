Elyndor — Weapon Damage Composition Amendment

Document: docs/source-of-truth/gameplay/09A_WEAPON_DAMAGE_COMPOSITION.md
System: Damage / Weapons
Status: Source of Truth Amendment
Version: 0.1

1. Назначение

Этот документ уточняет формулу player Auto Attack и заменяет для неё правило общей 90%–110% variance из раздела 5.1 `09_DAMAGE_AND_HEALING_SYSTEM.md`.

Цель — разделить две независимые величины:

- Weapon Damage — урон самого оружия;
- Attack Power Damage — вклад характеристики Attack Power.

Они рассчитываются отдельно и складываются только после собственного scaling.

2. Weapon Damage

Оружие задаёт server-authoritative диапазон:

```text
WeaponDamageMin
WeaponDamageMax
```

Для каждого подходящего weapon swing сервер выполняет deterministic RNG roll:

```text
RolledWeaponDamage = random(WeaponDamageMin, WeaponDamageMax)
```

После этого применяются только модификаторы, которые явно относятся к Weapon Damage:

```text
WeaponDamage = RolledWeaponDamage × WeaponDamageMultiplier
```

`WeaponDamageMultiplier` по умолчанию равен `1.0`.

Пример будущего таланта `+20% Weapon Damage`:

```text
WeaponDamageMultiplier = 1.20
```

Такой талант не увеличивает Attack Power и не изменяет AttackPowerCoefficient.

3. Attack Power Damage

Attack Power рассчитывается Stats System независимо от Weapon Damage.

Auto Attack получает вклад:

```text
AttackPowerDamage = FinalAttackPower × AttackPowerCoefficient
```

Таланты и эффекты на Attack Power изменяют `FinalAttackPower` или явно предусмотренный AP coefficient, но не меняют Weapon Damage range автоматически.

4. Итоговый базовый урон Auto Attack

Для обычной физической player Auto Attack:

```text
RolledWeaponDamage
× WeaponDamageMultiplier
= WeaponDamage

FinalAttackPower
× AttackPowerCoefficient
= AttackPowerDamage

RawAutoAttackDamage = WeaponDamage + AttackPowerDamage
```

Если конкретный AutoAttackProfile дополнительно использует Spell Power, его вклад остаётся отдельным:

```text
SpellPowerDamage = FinalSpellPower × SpellPowerCoefficient
RawAutoAttackDamage = WeaponDamage + AttackPowerDamage + SpellPowerDamage
```

После этого `RawAutoAttackDamage` проходит обычный Damage Pipeline: hit/dodge, critical, penetration, mitigation, outgoing/incoming modifiers, block, shields и HP.

5. Важное правило талантов

Weapon Damage и Attack Power являются разными scaling axes.

Поэтому допустимы независимые эффекты:

```text
+X% Weapon Damage
+X% Attack Power
+X% Auto Attack total damage
+X% Physical outgoing damage
```

Они не должны неявно подменять друг друга.

Это позволяет создавать таланты и предметные эффекты вроде:

- `+15% Weapon Damage` — усиливает только компонент оружия;
- `+10% Attack Power` — усиливает AP и все механики, которые масштабируются от AP;
- `+8% Auto Attack damage` — модифицирует уже собранную Auto Attack на соответствующем этапе;
- ability-specific Weapon Damage coefficient — отдельное будущее расширение для физических способностей.

6. Dual Wield

MainHand и OffHand используют собственные Weapon Damage ranges и собственный `WeaponDamageMultiplier` в соответствующем `AutoAttackProfile`.

Attack Power остаётся характеристикой персонажа и подставляется в каждый swing согласно коэффициенту профиля конкретной руки.

7. Совместимость

- существующие `WeaponDamageMin` / `WeaponDamageMax` остаются authoritative range;
- `WeaponDamageMultiplier = 1.0` сохраняет существующие значения weapon roll;
- legacy fixed base damage продолжает работать как fixed weapon/base component;
- новые weapon-only таланты могут изменять Weapon Damage без побочного усиления Attack Power;
- RNG остаётся server-authoritative и deterministic через `IGameRandom`.
