using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;
namespace Elyndor.Core.Combat.Sessions;
public sealed partial class CombatSession
{
private void ResolveCompanionExtraAttack(
CombatActorState target,
decimal multiplier,
string definitionId,
DateTimeOffset now)
{
if (_companion is null
|| _companion.Actor.IsDead
|| target.IsDead
|| multiplier <= 0)
{
return;
}
decimal attackPower = EffectEngine.CalculateStat(
_companion.Actor,
EffectStat.AttackPower,
_companion.Actor.Stats.AttackPower,
now);
decimal spellPower = EffectEngine.CalculateStat(
_companion.Actor,
EffectStat.SpellPower,
_companion.Actor.Stats.SpellPower,
now);
decimal ordinary =
((_companion.AutoAttack.BaseDamageMin ?? 0)
+ (_companion.AutoAttack.BaseDamageMax ?? 0)) / 2m
+ attackPower * _companion.AutoAttack.AttackPowerCoefficient
+ spellPower * _companion.AutoAttack.SpellPowerCoefficient;
ResolveCompanionFixedDamage(
target,
ordinary * multiplier,
_companion.AutoAttack.DamageType,
definitionId,
now);
}
private void ResolveCompanionFixedDamage(
CombatActorState target,
decimal amount,
DamageType type,
string definitionId,
DateTimeOffset now)
{
if (_companion is null
|| _companion.Actor.IsDead
|| target.IsDead
|| amount <= 0)
{
return;
}
decimal multiplier =
ResolveArcherCompanionDamageMultiplier(target, now);
DamageResult result = DamagePipeline.Resolve(
new DamageRequest(
_companion.Actor,
target,
amount,
type,
CanMiss: false,
CanDodge: false,
CanCrit: false,
MinimumDamage: 0,
DamageMultiplier: multiplier),
_random,
now);
ApplyKernelEvents(
result.Events,
_companion.Actor.ActorId,
target.ActorId,
definitionId);
}
private void ResolveOwnerExtraArrow(
CombatActorState target,
decimal multiplier,
string definitionId,
DateTimeOffset now)
{
if (target.IsDead || multiplier <= 0)
return;
decimal attackPower = EffectEngine.CalculateStat(
_player.Actor,
EffectStat.AttackPower,
_player.Actor.Stats.AttackPower,
now);
decimal baseDamage = _player.AutoAttack.BaseDamage
+ attackPower * _player.AutoAttack.AttackPowerCoefficient;
if (_player.AutoAttack.BaseDamageMin is { } min
&& _player.AutoAttack.BaseDamageMax is { } max)
{
baseDamage = (min + max) / 2m
+ attackPower * _player.AutoAttack.AttackPowerCoefficient;
}
DamageResult result = DamagePipeline.Resolve(
new DamageRequest(
_player.Actor,
target,
baseDamage * multiplier,
DamageType.Physical,
CanMiss: false,
CanDodge: false,
CanCrit: false,
MinimumDamage: 0),
_random,
now);
ApplyKernelEvents(
result.Events,
_player.Actor.ActorId,
target.ActorId,
definitionId,
_player.AutoAttack.WeaponHand,
_player.AutoAttack.WeaponDefinitionId);
}
private void ResolveOwnerAttackPowerDamage(
CombatActorState target,
decimal coefficient,
DamageType type,
string definitionId,
DateTimeOffset now,
bool canCrit)
{
if (target.IsDead || coefficient <= 0)
return;
decimal attackPower = EffectEngine.CalculateStat(
_player.Actor,
EffectStat.AttackPower,
_player.Actor.Stats.AttackPower,
now);
decimal damageMultiplier = 1;
if (HasOwnedPoison(target, now)
&& TryGetArcherHook(
"S-5-3",
"POISONED_TARGET_DAMAGE",
out ResolvedTalentEventHook poisoned))
{
damageMultiplier *= 1 + poisoned.Value / 100m;
}
DamageResult result = DamagePipeline.Resolve(
new DamageRequest(
_player.Actor,
target,
attackPower * coefficient,
type,
CanMiss: false,
CanDodge: false,
CanCrit: canCrit,
MinimumDamage: 0,
DamageMultiplier: damageMultiplier),
_random,
now);
ApplyKernelEvents(
result.Events,
_player.Actor.ActorId,
target.ActorId,
definitionId);
}
}
