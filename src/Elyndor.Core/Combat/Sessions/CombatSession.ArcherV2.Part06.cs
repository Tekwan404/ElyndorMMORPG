using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;
namespace Elyndor.Core.Combat.Sessions;
public sealed partial class CombatSession
{
private void ApplyArcherCriticalHooks(CombatEvent combatEvent)
{
if (!IsArcher)
return;
DateTimeOffset now = combatEvent.OccurredAtUtc;
if (combatEvent.SourceActorId == _player.Actor.ActorId)
{
CombatActorState? target =
ResolveEnemyActor(combatEvent.TargetActorId);
bool physicalShot =
IsPhysicalShotDefinition(combatEvent.DefinitionId);
if (physicalShot
&& target is not null
&& TryGetArcherHook(
"M-7-2",
"BROKEN_ARMOR",
out ResolvedTalentEventHook broken))
{
ApplyArcherEffect(
target,
new EffectDefinition(
BrokenArmorEffectId,
EffectKind.Debuff,
broken.Duration,
1,
EffectStackPolicy.Replace,
broken.Value,
SourceSpecific: true),
now);
}
if (physicalShot
&& TryGetArcherHook(
"M-6-2",
"DEADLY_STREAK",
out ResolvedTalentEventHook streak))
{
ApplyArcherEffect(
_player.Actor,
new EffectDefinition(
DeadlyStreakEffectId,
EffectKind.Buff,
streak.Duration,
Math.Max(1, streak.TriggerCount),
EffectStackPolicy.Stack,
streak.Value),
now);
}
if (physicalShot
&& target is not null
&& HasArcherEffect(target, HunterMarkEffectId, now)
&& TryGetArcherHook(
"M-6-4",
"PRECISE_TEMPO",
out ResolvedTalentEventHook tempo)
&& ArcherTalentCooldownReady(tempo.TalentId, now))
{
ReduceArcherCooldown(
"AIMED_SHOT",
TimeSpan.FromSeconds((double)tempo.Value),
now);
StartArcherTalentCooldown(
tempo.TalentId,
tempo.InternalCooldown,
now);
}
if (physicalShot
&& TryGetArcherHook(
"M-9-1",
"MASTER_ARROW_FOCUS",
out ResolvedTalentEventHook masterFocus)
&& ArcherTalentCooldownReady(
masterFocus.TalentId + ":FOCUS",
now))
{
ReduceArcherCooldown(
"SNIPER_FOCUS",
TimeSpan.FromSeconds((double)masterFocus.Value),
now);
StartArcherTalentCooldown(
masterFocus.TalentId + ":FOCUS",
masterFocus.InternalCooldown,
now);
}
if (_companion is not null && !_companion.Actor.IsDead)
{
if (TryGetArcherHook(
"B-6-3",
"OWNER_CRIT_PET_DAMAGE",
out ResolvedTalentEventHook petDamage))
{
ApplyCompanionMultiplier(
OwnerCritPetDamageEffectId,
EffectStat.OutgoingDamageMultiplier,
1 + petDamage.Value / 100m,
petDamage.Duration,
now);
}
if (TryGetArcherHook(
"B-7-3",
"OWNER_CRIT_PET_NEXT",
out ResolvedTalentEventHook petNext))
{
ApplyCompanionMultiplier(
BloodFangPetEffectId,
EffectStat.OutgoingDamageMultiplier,
1 + petNext.Value / 100m,
petNext.Duration,
now);
}
if (target is not null
&& TryGetArcherHook(
"B-9-1",
"BEAST_MASTER_EXTRA_ATTACK",
out ResolvedTalentEventHook extra)
&& ArcherTalentCooldownReady(extra.TalentId + ":EXTRA", now)
&& _random.NextUnit() < extra.Value / 100m)
{
ResolveCompanionExtraAttack(
target,
1,
extra.TalentId,
now);
StartArcherTalentCooldown(
extra.TalentId + ":EXTRA",
extra.InternalCooldown,
now);
}
}
}
if (_companion is not null
&& combatEvent.SourceActorId == _companion.Actor.ActorId)
{
if (TryGetArcherHook(
"B-5-1",
"FRENZY",
out ResolvedTalentEventHook frenzy)
&& _random.NextUnit() < frenzy.Value / 100m)
{
decimal bonusAttackSpeed = frenzy.SecondaryValue;
TimeSpan duration = frenzy.Duration;
if (TryGetArcherHook(
"B-7-1",
"PERFECT_FRENZY",
out ResolvedTalentEventHook perfect))
{
duration += TimeSpan.FromSeconds((double)perfect.Value);
bonusAttackSpeed += perfect.SecondaryValue;
}
ApplyCompanionMultiplier(
FrenzyEffectId,
EffectStat.AttackSpeed,
1 + bonusAttackSpeed / 100m,
duration,
now);
}
decimal ownerShotBonus = 0;
TimeSpan ownerShotDuration = TimeSpan.FromSeconds(5);
if (TryGetArcherHook(
"B-4-4",
"PET_CRIT_OWNER_SHOT",
out ResolvedTalentEventHook packHunter))
{
ownerShotBonus += packHunter.Value;
ownerShotDuration = packHunter.Duration;
}
if (TryGetArcherHook(
"B-7-3",
"PET_CRIT_OWNER_NEXT",
out ResolvedTalentEventHook bloodAndFang))
{
ownerShotBonus += bloodAndFang.Value;
if (bloodAndFang.Duration > ownerShotDuration)
ownerShotDuration = bloodAndFang.Duration;
}
if (TryGetArcherHook(
"B-9-1",
"BEAST_MASTER_OWNER_SHOT",
out ResolvedTalentEventHook beastMaster))
{
ownerShotBonus += beastMaster.Value;
if (beastMaster.Duration > ownerShotDuration)
ownerShotDuration = beastMaster.Duration;
}
if (ownerShotBonus > 0)
{
ApplyOneShotBuff(
PetCritShotEffectId,
ownerShotBonus,
0,
ownerShotDuration,
now);
}
}
}
}
