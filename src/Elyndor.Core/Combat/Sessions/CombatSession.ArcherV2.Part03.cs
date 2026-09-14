using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;
namespace Elyndor.Core.Combat.Sessions;
public sealed partial class CombatSession
{
private void TriggerFreezingTrap(DateTimeOffset now)
{
CombatParticipantDefinition? target = SelectedEnemy();
if (target is null)
return;
if (target.MonsterRank == MonsterRank.Boss)
{
ApplyArcherEffect(
target.Actor,
new EffectDefinition(
FreezingTrapBossEffectId,
EffectKind.StatModifier,
ArcherRuntimeDuration(
"FREEZING_TRAP",
"bossDurationSeconds"),
1,
EffectStackPolicy.Replace,
1 - ArcherRuntimeParameter(
"FREEZING_TRAP",
"bossAttackSpeedReductionPercent") / 100m,
ModifiedStat: EffectStat.AttackSpeed,
ModifierMode: EffectModifierMode.Multiplicative,
SourceSpecific: true),
now);
}
else
{
decimal durationSeconds =
ArcherRuntimeParameter("FREEZING_TRAP", "normalStunSeconds");
if (TryGetArcherHook(
"S-2-4",
"TRAP_CONTROL_DURATION",
out ResolvedTalentEventHook clever))
{
durationSeconds *= 1 + clever.Value / 100m;
}
ApplyArcherEffect(
target.Actor,
new EffectDefinition(
FreezingTrapStunEffectId,
EffectKind.Stun,
TimeSpan.FromSeconds((double)durationSeconds),
1,
EffectStackPolicy.Replace,
0,
SourceSpecific: true),
now);
}
OnTrapTriggered("FREEZING_TRAP", target.Actor, now);
}
private void TriggerImmolationTrap(DateTimeOffset now)
{
CombatActorState? target = SelectedEnemyActor();
if (target is null)
return;
decimal coefficient =
ArcherRuntimeParameter("IMMOLATION_TRAP", "attackPowerCoefficient")
* ArcherAbilityDamageMultiplier("IMMOLATION_TRAP");
decimal directCoefficient = coefficient * 0.5m;
decimal dotCoefficient = coefficient - directCoefficient;
ResolveOwnerAttackPowerDamage(
target,
directCoefficient,
DamageType.Physical,
"IMMOLATION_TRAP",
now,
canCrit: false);
ApplyAttackPowerDot(
target,
ImmolationTrapDotEffectId,
dotCoefficient,
ArcherRuntimeDuration("IMMOLATION_TRAP", "durationSeconds"),
ArcherRuntimeDuration("IMMOLATION_TRAP", "tickSeconds"),
now);
OnTrapTriggered("IMMOLATION_TRAP", target, now);
}
private void TriggerExplosiveTrap(DateTimeOffset now)
{
decimal damageMultiplier = ArcherAbilityDamageMultiplier("EXPLOSIVE_TRAP");
foreach (CombatParticipantDefinition enemy in _enemies.Where(item => !item.Actor.IsDead))
{
ResolveOwnerAttackPowerDamage(
enemy.Actor,
ArcherRuntimeParameter(
"EXPLOSIVE_TRAP",
"attackPowerCoefficient") * damageMultiplier,
DamageType.Physical,
"EXPLOSIVE_TRAP",
now,
canCrit: false);
ApplyAttackPowerDot(
enemy.Actor,
ExplosiveTrapDotEffectId,
ArcherRuntimeParameter(
"EXPLOSIVE_TRAP",
"burnCoefficient") * damageMultiplier,
ArcherRuntimeDuration(
"EXPLOSIVE_TRAP",
"burnDurationSeconds"),
ArcherRuntimeDuration(
"EXPLOSIVE_TRAP",
"tickSeconds"),
now);
OnTrapTriggered("EXPLOSIVE_TRAP", enemy.Actor, now);
}
}
private void ActivateDeterrence(DateTimeOffset now)
{
ApplyArcherEffect(
_player.Actor,
new EffectDefinition(
DeterrenceEffectId,
EffectKind.StatModifier,
ArcherRuntimeDuration("DETERRENCE", "durationSeconds"),
1,
EffectStackPolicy.Replace,
ArcherRuntimeParameter(
"DETERRENCE",
"incomingDamageMultiplier"),
ModifiedStat: EffectStat.IncomingDamageMultiplier,
ModifierMode: EffectModifierMode.Multiplicative,
SourceSpecific: true),
now);
if (TryGetArcherHook(
"S-8-3",
"PERFECT_DETERRENCE",
out _))
{
RemoveTalentEffects(
EffectEngine.RemoveByKind(
_player.Actor,
EffectKind.Stun,
now));
RemoveTalentEffects(
EffectEngine.RemoveByKind(
_player.Actor,
EffectKind.Silence,
now));
_playerRuntime.Cooldowns.Remove("FREEZING_TRAP");
}
}
}
