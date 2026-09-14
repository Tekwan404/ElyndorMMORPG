using Elyndor.Core.Characters;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Talents;
namespace Elyndor.Infrastructure.Combat;
internal static class ArcherCompanionRuntimeResolver
{
public static CombatParticipantDefinition Resolve(
CompanionProfileDefinition profile,
CharacterStats ownerStats,
int ownerLevel,
ResolvedTalentModifiers talents)
{
ArgumentNullException.ThrowIfNull(profile);
ArgumentNullException.ThrowIfNull(ownerStats);
ArgumentNullException.ThrowIfNull(talents);
bool physical = string.Equals(profile.Tag, "PHYSICAL_PET", StringComparison.Ordinal);
decimal maxHpPercent = 0;
decimal damagePercent = 0;
decimal damageReductionPercent = 0;
decimal criticalChanceBonus = 0;
decimal criticalDamageBonus = 0;
if (physical)
{
Add("B-1-1", valueForHp: true);
Add("B-1-2", valueForDamage: true);
Add("B-2-2", valueForDamageReduction: true);
Add("B-2-3", valueForCritChance: true);
Add("B-8-1", valueForCritChance: true, secondaryForCritDamage: true);
Add("B-9-1", valueForDamage: true, secondaryForHp: true);
}
decimal maxHp = (
profile.MaxHpBase
+ ownerStats.Stamina * profile.MaxHpPerOwnerStamina)
* (1 + maxHpPercent / 100m);
decimal attackPower =
ownerStats.AttackPower * profile.AttackPowerOwnerCoefficient;
decimal spellPower =
ownerStats.SpellPower * profile.SpellPowerOwnerCoefficient;
CombatStats stats = new(
ownerLevel,
profile.Accuracy,
profile.Dodge,
profile.CriticalChance + criticalChanceBonus,
profile.CriticalDamage + criticalDamageBonus / 100m,
profile.Armor,
profile.MagicResistance,
ArmorPenetration: 0,
MagicPenetration: 0,
AttackPower: attackPower,
SpellPower: spellPower);
CombatActorState actor = new(
Guid.CreateVersion7(),
Math.Max(1, maxHp),
Math.Max(1, maxHp),
0,
0,
stats,
new TalentCombatModifiers(
DamageDealtPercent: damagePercent,
IncomingPhysicalDamageReductionPercent: damageReductionPercent,
IncomingMagicalDamageReductionPercent: damageReductionPercent));
AutoAttackProfile autoAttack = new(
profile.AutoAttackInterval,
BaseDamage: 0,
AttackPowerCoefficient: physical ? 1 : 0,
ResourceOnHit: 0,
BaseDamageMin: profile.BaseDamageMin,
BaseDamageMax: profile.BaseDamageMax,
DamageType: physical ? DamageType.Physical : DamageType.Magical,
SpellPowerCoefficient: physical ? 0 : 1);
return new CombatParticipantDefinition(
actor,
CombatActorKind.Companion,
profile.Id,
profile.Name,
"NONE",
autoAttack,
new HashSet<string>(StringComparer.Ordinal));
void Add(
string talentId,
bool valueForHp = false,
bool secondaryForHp = false,
bool valueForDamage = false,
bool valueForDamageReduction = false,
bool valueForCritChance = false,
bool secondaryForCritDamage = false)
{
ResolvedTalentEventHook? hook = talents.EventHooks.FirstOrDefault(item =>
string.Equals(item.TalentId, talentId, StringComparison.Ordinal));
if (hook is null)
return;
if (valueForHp) maxHpPercent += hook.Value;
if (secondaryForHp) maxHpPercent += hook.SecondaryValue;
if (valueForDamage) damagePercent += hook.Value;
if (valueForDamageReduction) damageReductionPercent += hook.Value;
if (valueForCritChance) criticalChanceBonus += hook.Value;
if (secondaryForCritDamage) criticalDamageBonus += hook.SecondaryValue;
}
}
}
