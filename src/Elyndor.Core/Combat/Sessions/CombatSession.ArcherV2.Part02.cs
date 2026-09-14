using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;
namespace Elyndor.Core.Combat.Sessions;
public sealed partial class CombatSession
{
private void OnArcherAbilityResolved(
AbilityDefinition ability,
AbilityExecutionResult execution,
DateTimeOffset now)
{
if (!IsArcher || Status != CombatSessionStatus.Active)
return;
switch (ability.Id)
{
case "SNIPER_FOCUS":
ActivateSniperFocus(now);
return;
case "HEAVY_ARROW":
ArmHeavyArrow(now);
return;
case "COMMAND_ATTACK":
ResolveCommandAttack(now);
return;
case "MEND_PET":
MendPet(now);
return;
case "INTIMIDATION":
ResolveIntimidation(now);
return;
case "BESTIAL_WRATH":
ActivateBestialWrath(now);
return;
case "RETURN_TO_OWNER":
CleanseCompanion(now);
return;
case "SERPENT_STING":
ApplySerpentSting(now);
return;
case "FREEZING_TRAP":
TriggerFreezingTrap(now);
return;
case "IMMOLATION_TRAP":
TriggerImmolationTrap(now);
return;
case "EXPLOSIVE_TRAP":
TriggerExplosiveTrap(now);
return;
case "DETERRENCE":
ActivateDeterrence(now);
return;
case "WYVERN_STING":
ApplyWyvernSting(now);
return;
case "PREPARATION":
ActivatePreparation(now);
return;
}
if (string.Equals(ability.Id, "DISORIENTING_SHOT", StringComparison.Ordinal))
InterruptSelectedEnemy(now);
if (string.Equals(ability.Id, "SHOCKING_SHOT", StringComparison.Ordinal))
TryApplyShockingStun(now);
if (!IsPhysicalShotAbility(ability))
return;
bool hit = execution.Events.Any(item =>
item.Type == CombatEventType.DamageDealt
&& item.SourceActorId == _player.Actor.ActorId
&& item.Amount > 0);
CombatActorState? target = ResolveArcherExecutionEnemyTarget(execution)
?? SelectedEnemyActor();
if (!hit)
{
_archerShotSequence = 0;
_markedShotSequence = 0;
if (ability.ResourceCost > 0
&& TryGetArcherHook(
"M-7-3",
"MISS_REFUND",
out ResolvedTalentEventHook refund))
{
AddResource(
_player.Actor,
ability.ResourceCost * refund.Value / 100m,
now,
refund.TalentId);
}
return;
}
if (target is null)
return;
_lastOwnerHitTargetId = target.ActorId;
_lastOwnerHitAtUtc = now;
RegisterSuccessfulPhysicalShot(target, now);
if (string.Equals(ability.Id, "PIERCING_ARROW", StringComparison.Ordinal)
&& TryGetArcherHook(
"M-4-3",
"EXPOSED_DEFENSE",
out ResolvedTalentEventHook exposed))
{
ApplyArcherEffect(
_player.Actor,
new EffectDefinition(
ExposedDefenseEffectId,
EffectKind.Buff,
exposed.Duration,
Math.Max(1, exposed.TriggerCount),
EffectStackPolicy.Replace,
exposed.Value),
now);
ActiveEffect? applied =
FindArcherEffect(_player.Actor, ExposedDefenseEffectId, now);
if (applied is not null)
applied.Stacks = Math.Max(1, exposed.TriggerCount);
}
}
private void ActivateSniperFocus(DateTimeOffset now)
{
ApplyArcherEffect(
_player.Actor,
new EffectDefinition(
SniperFocusEffectId,
EffectKind.Buff,
ArcherRuntimeDuration("SNIPER_FOCUS", "durationSeconds"),
1,
EffectStackPolicy.Replace,
1),
now);
if (TryGetArcherHook(
"M-8-3",
"SNIPER_FOCUS_RESET",
out _))
{
_playerRuntime.Cooldowns.Remove("AIMED_SHOT");
_playerRuntime.Cooldowns.Remove("PIERCING_ARROW");
}
}
private void ArmHeavyArrow(DateTimeOffset now)
{
ApplyArcherEffect(
_player.Actor,
new EffectDefinition(
HeavyArrowEffectId,
EffectKind.Buff,
TimeSpan.FromHours(12),
1,
EffectStackPolicy.Replace,
ArcherRuntimeParameter("HEAVY_ARROW", "autoAttackDamageMultiplier")),
now);
}
private void ResolveCommandAttack(DateTimeOffset now)
{
if (_companion is null || _companion.Actor.IsDead || !IsPhysicalCompanion)
return;
CombatActorState? target = SelectedEnemyActor();
if (target is null || target.IsDead)
return;
decimal specialMultiplier = 1;
if (TryGetArcherHook(
"B-3-3",
"PET_SPECIAL_DAMAGE",
out ResolvedTalentEventHook training))
{
specialMultiplier *= 1 + training.Value / 100m;
}
if (_bestialWrathFirstCommandAvailable
&& HasArcherEffect(
_companion.Actor,
BestialWrathPetDamageEffectId,
now)
&& TryGetArcherHook(
"B-7-2",
"PRIMAL_COMMAND",
out ResolvedTalentEventHook primal))
{
specialMultiplier *= 1 + primal.Value / 100m;
_bestialWrathFirstCommandAvailable = false;
}
switch (CompanionArchetype)
{
case "GUARDIAN":
ResolveCompanionExtraAttack(
target,
1m * specialMultiplier,
"COMMAND_ATTACK",
now);
break;
case "TRAPPER":
ResolveCompanionExtraAttack(
target,
1m * specialMultiplier,
"COMMAND_ATTACK",
now);
ApplyArcherEffectFrom(
target,
_companion.Actor.ActorId,
new EffectDefinition(
"ARCHER_TRAPPER_COMMAND_STUN",
EffectKind.Stun,
TimeSpan.FromSeconds(
(double)ArcherRuntimeParameter(
"COMMAND_ATTACK",
"trapperStunDurationSeconds")),
1,
EffectStackPolicy.Replace,
0,
SourceSpecific: true),
now);
break;
default:
ResolveCompanionExtraAttack(
target,
ArcherRuntimeParameter(
"COMMAND_ATTACK",
"predatorDamageMultiplier") * specialMultiplier,
"COMMAND_ATTACK",
now);
ApplyCompanionAttackPowerDot(
target,
"ARCHER_PREDATOR_COMMAND_BLEED",
ArcherRuntimeParameter(
"COMMAND_ATTACK",
"predatorBleedPercent"),
ArcherRuntimeDuration(
"COMMAND_ATTACK",
"predatorBleedDurationSeconds"),
TimeSpan.FromSeconds(1),
now);
break;
}
if (TryGetArcherHook(
"B-5-4",
"COMMANDING_VOICE",
out ResolvedTalentEventHook voice))
{
ApplyCompanionMultiplier(
CommandVoiceEffectId,
EffectStat.OutgoingDamageMultiplier,
1 + voice.Value / 100m,
voice.Duration,
now);
}
if (TryGetArcherHook(
"B-8-2",
"COORDINATION",
out ResolvedTalentEventHook coordination))
{
ApplyOneShotBuff(
CoordinationEffectId,
coordination.Value,
coordination.SecondaryValue,
coordination.Duration,
now);
}
}
private void MendPet(DateTimeOffset now)
{
if (_companion is null || _companion.Actor.IsDead)
return;
decimal totalPercent = ArcherRuntimeParameter("MEND_PET", "healPercent");
if (TryGetArcherHook(
"B-3-1",
"IMPROVED_MEND",
out ResolvedTalentEventHook improved))
{
totalPercent *= 1 + improved.Value / 100m;
RemoveOneCompanionDebuff(now);
}
TimeSpan duration = ArcherRuntimeDuration("MEND_PET", "durationSeconds");
TimeSpan tick = ArcherRuntimeDuration("MEND_PET", "tickSeconds");
decimal ticks = Math.Max(
1m,
(decimal)(duration.TotalSeconds / tick.TotalSeconds));
decimal perTick = _companion.Actor.MaxHp * totalPercent / 100m / ticks;
ApplyArcherEffectFrom(
_companion.Actor,
_player.Actor.ActorId,
new EffectDefinition(
MendPetEffectId,
EffectKind.HealingOverTime,
duration,
1,
EffectStackPolicy.Replace,
perTick,
tick,
SourceSpecific: true),
now);
}
private void ResolveIntimidation(DateTimeOffset now)
{
if (_companion is null || _companion.Actor.IsDead)
return;
CombatParticipantDefinition? target = SelectedEnemy();
if (target is null)
return;
if (target.MonsterRank == MonsterRank.Boss)
{
ApplyArcherEffectFrom(
target.Actor,
_companion.Actor.ActorId,
new EffectDefinition(
IntimidationBossEffectId,
EffectKind.StatModifier,
ArcherRuntimeDuration("INTIMIDATION", "bossDurationSeconds"),
1,
EffectStackPolicy.Replace,
1 - ArcherRuntimeParameter(
"INTIMIDATION",
"bossAttackSpeedReductionPercent") / 100m,
ModifiedStat: EffectStat.AttackSpeed,
ModifierMode: EffectModifierMode.Multiplicative,
SourceSpecific: true),
now);
}
else
{
ApplyArcherEffectFrom(
target.Actor,
_companion.Actor.ActorId,
new EffectDefinition(
IntimidationStunEffectId,
EffectKind.Stun,
ArcherRuntimeDuration("INTIMIDATION", "normalStunSeconds"),
1,
EffectStackPolicy.Replace,
0,
SourceSpecific: true),
now);
}
}
private void ActivateBestialWrath(DateTimeOffset now)
{
if (_companion is null || _companion.Actor.IsDead || !IsPhysicalCompanion)
return;
TimeSpan duration =
ArcherRuntimeDuration("BESTIAL_WRATH", "durationSeconds");
ApplyCompanionMultiplier(
BestialWrathPetDamageEffectId,
EffectStat.OutgoingDamageMultiplier,
ArcherRuntimeParameter(
"BESTIAL_WRATH",
"companionDamageMultiplier"),
duration,
now);
ApplyCompanionMultiplier(
BestialWrathPetAttackSpeedEffectId,
EffectStat.AttackSpeed,
ArcherRuntimeParameter(
"BESTIAL_WRATH",
"companionAttackSpeedMultiplier"),
duration,
now);
RemoveCompanionControls(now);
bool resetCommand =
TryGetArcherHook("B-7-2", "PRIMAL_COMMAND", out _)
|| TryGetArcherHook("B-8-3", "UNSTOPPABLE_PACK", out _);
if (resetCommand)
_playerRuntime.Cooldowns.Remove("COMMAND_ATTACK");
_bestialWrathFirstCommandAvailable =
TryGetArcherHook("B-7-2", "PRIMAL_COMMAND", out _);
}
private void CleanseCompanion(DateTimeOffset now)
{
if (_companion is null || _companion.Actor.IsDead)
return;
RemoveTalentEffects(
EffectEngine.RemoveByKind(
_companion.Actor,
EffectKind.Stun,
now));
RemoveTalentEffects(
EffectEngine.RemoveByKind(
_companion.Actor,
EffectKind.Silence,
now));
RemoveOneCompanionDebuff(now);
}
private void ApplySerpentSting(DateTimeOffset now)
{
CombatActorState? target = SelectedEnemyActor();
if (target is null)
return;
decimal coefficient =
ArcherRuntimeParameter("SERPENT_STING", "attackPowerCoefficient");
TimeSpan duration =
ArcherRuntimeDuration("SERPENT_STING", "durationSeconds");
TimeSpan tick =
ArcherRuntimeDuration("SERPENT_STING", "tickSeconds");
TalentAbilityModifiers modifiers =
_playerTalents.Abilities.GetValueOrDefault("SERPENT_STING")
?? new TalentAbilityModifiers();
coefficient *= 1 + modifiers.DamagePercentBonus / 100m;
duration += TimeSpan.FromSeconds(
(double)modifiers.EffectDurationSecondsBonus);
decimal carriedDamage = 0;
if (TryGetArcherHook(
"S-3-3",
"TOXICOLOGY_CARRY",
out _))
{
ActiveEffect? old = FindArcherEffect(target, SerpentStingEffectId, now);
if (old is not null && old.Definition.TickInterval is { } oldTick)
{
decimal seconds =
Math.Max(0, (decimal)(old.ExpiresAtUtc - now).TotalSeconds);
decimal remainingTicks = Math.Ceiling(
seconds / (decimal)oldTick.TotalSeconds);
carriedDamage = old.Definition.Magnitude
* old.Stacks
* remainingTicks;
}
}
ApplyAttackPowerDot(
target,
SerpentStingEffectId,
coefficient,
duration,
tick,
now,
carriedDamage);
}
private void ApplyWyvernSting(DateTimeOffset now)
{
CombatActorState? target = SelectedEnemyActor();
if (target is null)
return;
TalentAbilityModifiers modifiers =
_playerTalents.Abilities.GetValueOrDefault("WYVERN_STING")
?? new TalentAbilityModifiers();
decimal coefficient =
ArcherRuntimeParameter("WYVERN_STING", "attackPowerCoefficient")
* (1 + modifiers.DamagePercentBonus / 100m);
TimeSpan duration =
ArcherRuntimeDuration("WYVERN_STING", "durationSeconds");
TimeSpan tick =
ArcherRuntimeDuration("WYVERN_STING", "tickSeconds");
ApplyAttackPowerDot(
target,
WyvernStingEffectId,
coefficient,
duration,
tick,
now);
ApplyArcherEffect(
target,
new EffectDefinition(
WyvernWeaknessEffectId,
EffectKind.StatModifier,
ArcherRuntimeDuration(
"WYVERN_STING",
"damageReductionSeconds"),
1,
EffectStackPolicy.Replace,
1 - ArcherRuntimeParameter(
"WYVERN_STING",
"damageReductionPercent") / 100m,
ModifiedStat: EffectStat.OutgoingDamageMultiplier,
ModifierMode: EffectModifierMode.Multiplicative,
SourceSpecific: true),
now);
}
}
