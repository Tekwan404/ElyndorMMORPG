using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;
namespace Elyndor.Core.Combat.Sessions;
public sealed partial class CombatSession
{
private void ApplyArcherAutoAttackResolved(
CombatParticipantDefinition target,
AutoAttackProfile profile,
decimal ordinaryBaseDamage,
DamageResult damage,
ArcherAutoAttackModifier modifier,
DateTimeOffset now)
{
if (!IsArcher)
return;
ConsumeArcherStackedEffect(
_player.Actor,
ExposedDefenseEffectId,
1,
now);
RemoveArcherEffect(
_player.Actor,
PetCritShotEffectId,
now);
RemoveArcherEffect(
_player.Actor,
CounterShotEffectId,
now);
RemoveArcherEffect(
_player.Actor,
TrapNextShotEffectId,
now);
RemoveArcherEffect(
_player.Actor,
SurvivalMasterShotEffectId,
now);
if (modifier.Heavy)
RemoveArcherEffect(_player.Actor, HeavyArrowEffectId, now);
if (damage.Avoidance != DamageAvoidance.None || damage.HpDamage <= 0)
{
_archerShotSequence = 0;
_markedShotSequence = 0;
return;
}
_lastOwnerHitTargetId = target.Actor.ActorId;
_lastOwnerHitAtUtc = now;
RegisterSuccessfulPhysicalShot(target.Actor, now);
TryProcHawk(
"M-2-4",
"HAWK_SPIRIT",
HawkSpiritEffectId,
now);
TryProcHawk(
"B-1-3",
"IMPROVED_HAWK",
BeastHawkEffectId,
now);
}
private decimal ResolveArcherCompanionDamageMultiplier(
CombatActorState target,
DateTimeOffset now)
{
if (!IsArcher || _companion is null || _companion.Actor.IsDead)
return 1;
decimal multiplier = 1;
if (IsSharedTarget(target.ActorId, now)
&& TryGetArcherHook(
"B-3-2",
"JOINT_HUNT",
out ResolvedTalentEventHook joint))
{
multiplier *= 1 + joint.Value / 100m;
}
if (ArcherHpPercent(target) < 30
&& TryGetArcherHook(
"B-6-1",
"PET_EXECUTE",
out ResolvedTalentEventHook execute))
{
multiplier *= 1 + execute.Value / 100m;
}
return multiplier;
}
private void RegisterSuccessfulPhysicalShot(
CombatActorState target,
DateTimeOffset now)
{
_archerShotSequence++;
if (TryGetArcherHook(
"M-4-4",
"COMBAT_RHYTHM",
out ResolvedTalentEventHook rhythm)
&& _archerShotSequence % Math.Max(1, rhythm.TriggerCount) == 0)
{
AddResource(_player.Actor, rhythm.Value, now, rhythm.TalentId);
}
if (HasArcherEffect(target, HunterMarkEffectId, now)
&& TryGetArcherHook(
"M-9-1",
"MASTER_ARROW_EXTRA",
out ResolvedTalentEventHook master))
{
_markedShotSequence++;
if (_markedShotSequence % Math.Max(1, master.TriggerCount) == 0)
{
ResolveOwnerExtraArrow(
target,
master.Value / 100m,
master.TalentId,
now);
}
}
else
{
_markedShotSequence = 0;
}
}
private void TryProcHawk(
string talentId,
string targetId,
string effectId,
DateTimeOffset now)
{
if (!TryGetArcherHook(
talentId,
targetId,
out ResolvedTalentEventHook hawk)
|| _random.NextUnit() >= hawk.ChancePercent / 100m)
{
return;
}
ApplyArcherEffect(
_player.Actor,
new EffectDefinition(
effectId,
EffectKind.StatModifier,
hawk.Duration,
1,
EffectStackPolicy.Replace,
1 + hawk.Value / 100m,
ModifiedStat: EffectStat.AttackSpeed,
ModifierMode: EffectModifierMode.Multiplicative,
SourceSpecific: true),
now);
}
}
