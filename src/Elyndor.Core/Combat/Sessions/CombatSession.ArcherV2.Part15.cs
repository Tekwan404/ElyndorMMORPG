using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;
namespace Elyndor.Core.Combat.Sessions;
public sealed partial class CombatSession
{
private decimal ArcherRuntimeParameter(
string abilityId,
string parameterName)
{
if (!_abilities.TryGetValue(abilityId, out AbilityDefinition? ability)
|| ability.RuntimeParameters is null
|| !ability.RuntimeParameters.TryGetValue(parameterName, out decimal value))
{
throw new InvalidOperationException(
$"Archer ability '{abilityId}' is missing required runtime parameter "
+ $"'{parameterName}'.");
}
return value;
}
private TimeSpan ArcherRuntimeDuration(
string abilityId,
string parameterName) =>
TimeSpan.FromSeconds(
(double)ArcherRuntimeParameter(abilityId, parameterName));
private decimal ArcherAbilityDamageMultiplier(string abilityId)
{
TalentAbilityModifiers modifiers =
_playerTalents.Abilities.GetValueOrDefault(abilityId)
?? new TalentAbilityModifiers();
return 1 + modifiers.DamagePercentBonus / 100m;
}
private ActiveEffect? FindArcherEffect(
CombatActorState actor,
string effectId,
DateTimeOffset now) =>
actor.ActiveEffects.FirstOrDefault(effect =>
string.Equals(effect.Definition.Id, effectId, StringComparison.Ordinal)
&& effect.SourceId == _player.Actor.ActorId
&& effect.ExpiresAtUtc > now);
private bool HasArcherEffect(
CombatActorState actor,
string effectId,
DateTimeOffset now) =>
FindArcherEffect(actor, effectId, now) is not null;
private void ApplyArcherEffect(
CombatActorState target,
EffectDefinition effect,
DateTimeOffset now) =>
ApplyArcherEffectFrom(
target,
_player.Actor.ActorId,
effect,
now);
private void ApplyArcherEffectFrom(
CombatActorState target,
Guid sourceId,
EffectDefinition effect,
DateTimeOffset now) =>
ApplyTalentEffect(target, sourceId, effect, now);
private void RemoveArcherEffect(
CombatActorState target,
string effectId,
DateTimeOffset now) =>
RemoveArcherEffectFrom(
target,
effectId,
_player.Actor.ActorId,
now);
private void RemoveArcherEffectFrom(
CombatActorState target,
string effectId,
Guid sourceId,
DateTimeOffset now) =>
RemoveTalentEffects(
EffectEngine.RemoveOwned(
target,
effectId,
sourceId,
now));
private void RemoveOneCompanionDebuff(DateTimeOffset now)
{
if (_companion is null)
return;
ActiveEffect? negative =
_companion.Actor.ActiveEffects
.Where(effect =>
effect.ExpiresAtUtc > now
&& effect.SourceId != _player.Actor.ActorId
&& effect.SourceId != _companion.Actor.ActorId
&& effect.Definition.Kind is
EffectKind.Debuff
or EffectKind.DamageOverTime
or EffectKind.Silence
or EffectKind.Stun)
.OrderBy(effect => effect.AppliedAtUtc)
.FirstOrDefault();
if (negative is null)
return;
RemoveTalentEffects(
EffectEngine.RemoveOwned(
_companion.Actor,
negative.Definition.Id,
negative.SourceId,
now));
}
private void RemoveCompanionControls(DateTimeOffset now)
{
if (_companion is null)
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
}
private void ReduceArcherCooldown(
string abilityId,
TimeSpan amount,
DateTimeOffset now)
{
if (!_playerRuntime.Cooldowns.TryGetValue(
abilityId,
out DateTimeOffset readyAt)
|| readyAt <= now)
{
return;
}
DateTimeOffset reduced = readyAt - amount;
if (reduced <= now)
_playerRuntime.Cooldowns.Remove(abilityId);
else
_playerRuntime.Cooldowns[abilityId] = reduced;
}
}
