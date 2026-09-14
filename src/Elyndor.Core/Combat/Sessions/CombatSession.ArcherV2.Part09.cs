using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;
namespace Elyndor.Core.Combat.Sessions;
public sealed partial class CombatSession
{
private ArcherAutoAttackModifier ResolveArcherAutoAttackModifier(
CombatParticipantDefinition target,
decimal baseDamage,
DateTimeOffset now)
{
if (!IsArcher)
return new(1, 0, 0, 0, 0, false, false);
decimal multiplier = 1;
decimal armorPenetration = 0;
decimal accuracyBonus = 0;
decimal criticalChanceBonus = 0;
decimal criticalDamageBonus = 0;
if (TryGetArcherHook(
"M-1-1",
"PHYSICAL_SHOT_CRIT",
out ResolvedTalentEventHook lethalShooting))
{
criticalChanceBonus += lethalShooting.Value;
}
if (TryGetArcherHook(
"M-3-2",
"PHYSICAL_SHOT_CRIT_DAMAGE",
out ResolvedTalentEventHook deadlyShots))
{
criticalDamageBonus += deadlyShots.Value;
}
ActiveEffect? heavy =
FindArcherEffect(_player.Actor, HeavyArrowEffectId, now);
if (heavy is not null)
multiplier *= heavy.Definition.Magnitude;
if (HasArcherEffect(_player.Actor, SniperFocusEffectId, now))
{
accuracyBonus += ArcherRuntimeParameter(
"SNIPER_FOCUS",
"accuracyBonus");
criticalChanceBonus += ArcherRuntimeParameter(
"SNIPER_FOCUS",
"criticalChanceBonus");
armorPenetration += ArcherRuntimeParameter(
"SNIPER_FOCUS",
"armorPenetrationBonus");
}
if (TryGetArcherHook(
"M-5-2",
"BOW_DAMAGE",
out ResolvedTalentEventHook bowDamage))
{
multiplier *= 1 + bowDamage.Value / 100m;
}
bool marked = HasArcherEffect(target.Actor, HunterMarkEffectId, now);
if (marked)
{
ActiveEffect mark =
FindArcherEffect(target.Actor, HunterMarkEffectId, now)!;
multiplier *= 1 + mark.Definition.Magnitude / 100m;
if (TryGetArcherHook(
"M-5-3",
"MARKED_VICTIM",
out ResolvedTalentEventHook victim))
{
multiplier *= 1 + victim.Value / 100m;
}
}
ActiveEffect? exposed =
FindArcherEffect(_player.Actor, ExposedDefenseEffectId, now);
if (exposed is not null)
armorPenetration += exposed.Definition.Magnitude / 100m;
ActiveEffect? broken =
FindArcherEffect(target.Actor, BrokenArmorEffectId, now);
if (broken is not null)
armorPenetration += broken.Definition.Magnitude / 100m;
ActiveEffect? streak =
FindArcherEffect(_player.Actor, DeadlyStreakEffectId, now);
if (streak is not null)
criticalDamageBonus += streak.Definition.Magnitude * streak.Stacks;
ApplyExistingOneShotDamage(
PetCritShotEffectId,
ref multiplier,
now);
ApplyExistingOneShotDamage(
CounterShotEffectId,
ref multiplier,
now);
ApplyExistingOneShotDamage(
TrapNextShotEffectId,
ref multiplier,
now);
ApplyExistingOneShotDamage(
SurvivalMasterShotEffectId,
ref multiplier,
now);
ActiveEffect? trapShot =
FindArcherEffect(_player.Actor, TrapNextShotEffectId, now);
if (trapShot is not null)
criticalChanceBonus += trapShot.RemainingMagnitude;
bool poisoned = HasOwnedPoison(target.Actor, now);
if (poisoned
&& TryGetArcherHook(
"S-5-3",
"POISONED_TARGET_DAMAGE",
out ResolvedTalentEventHook poisonDamage))
{
multiplier *= 1 + poisonDamage.Value / 100m;
}
if (poisoned
&& TryGetArcherHook(
"S-7-3",
"POISONED_TARGET_CRIT",
out ResolvedTalentEventHook poisonCrit))
{
criticalChanceBonus += poisonCrit.Value;
}
if ((poisoned || HasArcherEffect(target.Actor, TrapRecentEffectId, now))
&& TryGetArcherHook(
"S-9-1",
"SURVIVAL_MASTER_CRIT",
out ResolvedTalentEventHook masterCrit))
{
criticalChanceBonus += masterCrit.Value;
}
if (IsSharedTarget(target.Actor.ActorId, now)
&& TryGetArcherHook(
"B-3-2",
"JOINT_HUNT",
out ResolvedTalentEventHook joint))
{
multiplier *= 1 + joint.Value / 100m;
}
return new(
multiplier,
armorPenetration,
accuracyBonus,
criticalChanceBonus,
criticalDamageBonus,
heavy is not null,
false);
}
}
