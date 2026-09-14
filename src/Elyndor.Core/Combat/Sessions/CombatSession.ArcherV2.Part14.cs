using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;
namespace Elyndor.Core.Combat.Sessions;
public sealed partial class CombatSession
{
private static void ApplyOneShotModifier(
string effectId,
ref decimal damageMultiplier,
ref decimal resourceCost,
CombatActorState actor,
Guid sourceActorId,
DateTimeOffset now)
{
ActiveEffect? effect = actor.ActiveEffects.FirstOrDefault(item =>
string.Equals(item.Definition.Id, effectId, StringComparison.Ordinal)
&& item.SourceId == sourceActorId
&& item.ExpiresAtUtc > now);
if (effect is null)
return;
damageMultiplier *= 1 + effect.Definition.Magnitude / 100m;
resourceCost *= Math.Max(
0,
1 - effect.RemainingMagnitude / 100m);
}
private void ApplyOneShotModifier(
string effectId,
ref decimal damageMultiplier,
ref decimal resourceCost,
DateTimeOffset now) =>
ApplyOneShotModifier(
effectId,
ref damageMultiplier,
ref resourceCost,
_player.Actor,
_player.Actor.ActorId,
now);
private void ApplyExistingOneShotDamage(
string effectId,
ref decimal multiplier,
DateTimeOffset now)
{
ActiveEffect? effect =
FindArcherEffect(_player.Actor, effectId, now);
if (effect is not null)
multiplier *= 1 + effect.Definition.Magnitude / 100m;
}
private bool IsSharedTarget(Guid targetId, DateTimeOffset now) =>
_lastOwnerHitTargetId == targetId
&& _lastCompanionHitTargetId == targetId
&& _lastOwnerHitAtUtc is { }
ownerAt
&& _lastCompanionHitAtUtc is { }
companionAt
&& Math.Abs((ownerAt - companionAt).TotalSeconds) <= 2
&& Math.Abs((now - ownerAt).TotalSeconds) <= 4;
private bool SelectedTargetHasHunterMark(DateTimeOffset now) =>
SelectedEnemyActor() is { }
target
&& HasArcherEffect(target, HunterMarkEffectId, now);
private CombatParticipantDefinition? SelectedEnemy() =>
_enemiesById.GetValueOrDefault(_selectedTargetActorId);
private CombatActorState? SelectedEnemyActor() =>
SelectedEnemy()?.Actor;
private CombatActorState? ResolveEnemyActor(Guid? actorId) =>
actorId is { }
id
&& _enemiesById.TryGetValue(id, out CombatParticipantDefinition? target)
? target.Actor
: null;
private CombatActorState? ResolveArcherExecutionEnemyTarget(
AbilityExecutionResult execution)
{
foreach (CombatEvent item in execution.Events.Reverse())
{
CombatActorState? target = ResolveEnemyActor(item.TargetActorId);
if (target is not null)
return target;
}
return null;
}
private bool HasOwnedPoison(CombatActorState target, DateTimeOffset now) =>
HasArcherEffect(target, SerpentStingEffectId, now)
|| HasArcherEffect(target, WyvernStingEffectId, now);
private bool TryGetArcherHook(
string talentId,
out ResolvedTalentEventHook hook)
{
if (string.Equals(talentId, "B-5-2", StringComparison.Ordinal)
&& IsArcher)
{
hook = _playerTalents.EventHooks.FirstOrDefault(item =>
string.Equals(item.TalentId, "B-5-3", StringComparison.Ordinal)
&& string.Equals(
item.TargetId,
"PET_AOE_REDUCTION",
StringComparison.Ordinal))!;
if (hook is not null)
return true;
}
hook = IsArcher
? _playerTalents.EventHooks.FirstOrDefault(item =>
string.Equals(item.TalentId, talentId, StringComparison.Ordinal))!
: null!;
return hook is not null;
}
private bool TryGetArcherHook(
string talentId,
string targetId,
out ResolvedTalentEventHook hook)
{
hook = IsArcher
? _playerTalents.EventHooks.FirstOrDefault(item =>
string.Equals(item.TalentId, talentId, StringComparison.Ordinal)
&& string.Equals(item.TargetId, targetId, StringComparison.Ordinal))!
: null!;
return hook is not null;
}
}
