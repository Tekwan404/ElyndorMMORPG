using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;
namespace Elyndor.Core.Combat.Sessions;
public sealed partial class CombatSession
{
private void ApplyArcherIncomingCriticalHooks(CombatEvent combatEvent)
{
}
private void ApplyArcherDamageTakenHooks(CombatEvent combatEvent)
{
if (!IsArcher
|| combatEvent.TargetActorId != _player.Actor.ActorId
|| combatEvent.Amount <= 0
|| combatEvent.IsPeriodic)
{
return;
}
DateTimeOffset now = combatEvent.OccurredAtUtc;
if (HasArcherEffect(_player.Actor, DeterrenceEffectId, now)
&& TryGetArcherHook(
"S-4-4",
"DETERRENCE_COUNTER",
out ResolvedTalentEventHook counter))
{
ApplyOneShotBuff(
CounterShotEffectId,
counter.Value,
0,
counter.Duration,
now);
}
if (_companion is null
|| _companion.Actor.IsDead
|| !TryGetArcherHook(
"B-6-2",
"PET_INTERCEPT",
out ResolvedTalentEventHook intercept)
|| combatEvent.Amount
<= _player.Actor.MaxHp * intercept.Threshold / 100m
|| !ArcherTalentCooldownReady(intercept.TalentId, now))
{
return;
}
decimal redirected = combatEvent.Amount * intercept.Value / 100m;
RestoreArcherHp(
_player.Actor,
redirected,
now,
intercept.TalentId);
DamageCompanionDirect(
redirected,
intercept.TalentId,
now);
StartArcherTalentCooldown(
intercept.TalentId,
intercept.InternalCooldown,
now);
}
private void ApplyArcherHealingHooks(CombatEvent combatEvent)
{
if (!IsArcher
|| combatEvent.TargetActorId != _player.Actor.ActorId
|| combatEvent.Amount <= 0
|| _companion is null
|| _companion.Actor.IsDead
|| !TryGetArcherHook(
"B-7-4",
"OWNER_HEAL_PET",
out ResolvedTalentEventHook oneBlood))
{
return;
}
RestoreArcherHp(
_companion.Actor,
combatEvent.Amount * oneBlood.Value / 100m,
combatEvent.OccurredAtUtc,
oneBlood.TalentId);
}
private void ApplyArcherCompanionDamageHooks(CombatEvent combatEvent)
{
if (!IsArcher
|| _companion is null
|| combatEvent.SourceActorId != _companion.Actor.ActorId
|| combatEvent.Amount <= 0
|| combatEvent.TargetActorId is not { } targetId)
{
return;
}
DateTimeOffset now = combatEvent.OccurredAtUtc;
_lastCompanionHitTargetId = targetId;
_lastCompanionHitAtUtc = now;
if (TryGetArcherHook(
"B-1-4",
"PET_FOCUS",
out ResolvedTalentEventHook focus)
&& ArcherTalentCooldownReady(focus.TalentId, now)
&& _random.NextUnit() < focus.SecondaryValue / 100m)
{
AddResource(
_player.Actor,
focus.Value,
now,
focus.TalentId);
StartArcherTalentCooldown(
focus.TalentId,
focus.InternalCooldown,
now);
}
RemoveArcherEffectFrom(
_companion.Actor,
BloodFangPetEffectId,
_player.Actor.ActorId,
now);
}
private void ApplyArcherEnemyKilledHooks(DateTimeOffset now)
{
}
private void ApplyArcherResourceThresholdHooks(CombatEvent combatEvent)
{
}
private void SyncArcherConditionalEffects(DateTimeOffset now)
{
if (!IsArcher)
return;
SyncSurvivalDefenses(now);
SyncTrueshotAura(now);
SyncSpiritBond(now);
SyncBestialWrathControlImmunity(now);
}
}
