namespace Elyndor.Core.Characters;

public sealed record CharacterStats(
    decimal Strength,
    decimal Agility,
    decimal Intellect,
    decimal Stamina,
    decimal MaxHp,
    decimal AttackPower,
    decimal SpellPower,
    decimal CriticalChance,
    decimal CriticalDamage,
    decimal Accuracy,
    decimal ArmorPenetration,
    decimal MagicPenetration,
    decimal AttackSpeed,
    decimal Armor,
    decimal MagicResistance,
    decimal Dodge);
