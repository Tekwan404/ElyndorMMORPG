using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;
namespace Elyndor.Core.Combat.Sessions;
public sealed partial class CombatSession
{
private void ApplyCompanionAttackPowerDot(
CombatActorState target,
string effectId,
decimal totalPercent,
TimeSpan duration,
TimeSpan tickInterval,
DateTimeOffset now)
{
if (_companion is null
|| target.IsDead
|| duration <= TimeSpan.Zero
|| tickInterval <= TimeSpan.Zero)
{
return;
}
decimal ticks = Math.Max(
1m,
(decimal)(duration.TotalSeconds / tickInterval.TotalSeconds));
decimal attackPower = EffectEngine.CalculateStat(
_companion.Actor,
EffectStat.AttackPower,
_companion.Actor.Stats.AttackPower,
now);
decimal tickDamage =
attackPower * totalPercent / 100m / ticks;
ApplyArcherEffectFrom(
target,
_companion.Actor.ActorId,
new EffectDefinition(
effectId,
EffectKind.DamageOverTime,
duration,
1,
EffectStackPolicy.Replace,
tickDamage,
tickInterval,
SourceSpecific: true,
PeriodicDamageType: DamageType.Physical),
now);
}
private void DamageCompanionDirect(
decimal amount,
string definitionId,
DateTimeOffset now)
{
if (_companion is null
|| _companion.Actor.IsDead
|| amount <= 0)
{
return;
}
DamageResult result = DamagePipeline.Resolve(
new DamageRequest(
_player.Actor,
_companion.Actor,
amount,
DamageType.True,
CanMiss: false,
CanDodge: false,
CanCrit: false,
IgnoreShields: true,
MinimumDamage: 0),
_random,
now);
ApplyKernelEvents(
result.Events,
_player.Actor.ActorId,
_companion.Actor.ActorId,
definitionId);
}
private void RestoreArcherHp(
CombatActorState actor,
decimal amount,
DateTimeOffset now,
string definitionId)
{
decimal before = actor.CurrentHp;
actor.ApplyHealing(amount);
decimal actual = actor.CurrentHp - before;
if (actual <= 0)
return;
Append(new CombatEvent(
CombatEventType.HealingApplied,
now,
actor.ActorId,
definitionId,
actual,
SourceActorId: _player.Actor.ActorId,
TargetActorId: actor.ActorId));
}
private void ApplyCompanionMultiplier(
string effectId,
EffectStat stat,
decimal multiplier,
TimeSpan duration,
DateTimeOffset now)
{
if (_companion is null
|| _companion.Actor.IsDead
|| duration <= TimeSpan.Zero)
{
return;
}
ApplyArcherEffectFrom(
_companion.Actor,
_player.Actor.ActorId,
new EffectDefinition(
effectId,
EffectKind.StatModifier,
duration,
1,
EffectStackPolicy.Replace,
multiplier,
ModifiedStat: stat,
ModifierMode: EffectModifierMode.Multiplicative,
SourceSpecific: true),
now);
}
private void EnsureArcherPlayerMultiplier(
string effectId,
EffectStat stat,
decimal multiplier,
DateTimeOffset now,
TimeSpan? duration = null)
{
ApplyArcherEffect(
_player.Actor,
new EffectDefinition(
effectId,
EffectKind.StatModifier,
duration ?? TimeSpan.FromHours(12),
1,
EffectStackPolicy.Replace,
multiplier,
ModifiedStat: stat,
ModifierMode: EffectModifierMode.Multiplicative,
SourceSpecific: true),
now);
}
private void ApplyOneShotBuff(
string effectId,
decimal damagePercent,
decimal resourceCostReductionPercent,
TimeSpan duration,
DateTimeOffset now)
{
ApplyArcherEffect(
_player.Actor,
new EffectDefinition(
effectId,
EffectKind.Buff,
duration <= TimeSpan.Zero ? TimeSpan.FromSeconds(5) : duration,
1,
EffectStackPolicy.Replace,
damagePercent),
now);
ActiveEffect? effect =
FindArcherEffect(_player.Actor, effectId, now);
if (effect is not null)
effect.RemainingMagnitude = resourceCostReductionPercent;
}
}
