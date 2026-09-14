using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;
namespace Elyndor.Core.Combat.Sessions;
public sealed partial class CombatSession
{
private void InterruptSelectedEnemy(DateTimeOffset now)
{
CombatParticipantDefinition? target = SelectedEnemy();
if (target is null
|| !_enemyRuntimes.TryGetValue(target.Actor.ActorId, out CombatRuntimeState? runtime)
|| runtime.ActiveCast is null)
{
return;
}
AbilityExecutionResult interrupted =
AbilityEngine.Interrupt(runtime, now, TimeSpan.Zero);
if (interrupted.Succeeded)
{
ApplyKernelEvents(
interrupted.Events,
_player.Actor.ActorId,
target.Actor.ActorId,
"DISORIENTING_SHOT");
}
}
private void TryApplyShockingStun(DateTimeOffset now)
{
CombatParticipantDefinition? target = SelectedEnemy();
if (target is null || target.MonsterRank == MonsterRank.Boss)
return;
if (_random.NextUnit() >= 0.10m)
return;
ApplyArcherEffect(
target.Actor,
new EffectDefinition(
ShockingShotStunEffectId,
EffectKind.Stun,
TimeSpan.FromSeconds(0.75),
1,
EffectStackPolicy.Replace,
0,
SourceSpecific: true),
now);
}
}
