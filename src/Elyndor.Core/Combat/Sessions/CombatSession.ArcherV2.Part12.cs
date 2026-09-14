using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;
namespace Elyndor.Core.Combat.Sessions;
public sealed partial class CombatSession
{
private void ApplyAttackPowerDot(
CombatActorState target,
string effectId,
decimal coefficient,
TimeSpan duration,
TimeSpan tickInterval,
DateTimeOffset now,
decimal carriedDamage = 0)
{
if (target.IsDead
|| coefficient <= 0
|| duration <= TimeSpan.Zero
|| tickInterval <= TimeSpan.Zero)
{
return;
}
decimal ticks = Math.Max(
1m,
(decimal)(duration.TotalSeconds / tickInterval.TotalSeconds));
decimal attackPower = EffectEngine.CalculateStat(
_player.Actor,
EffectStat.AttackPower,
_player.Actor.Stats.AttackPower,
now);
decimal totalDamage = attackPower * coefficient + carriedDamage;
decimal tickDamage = totalDamage / ticks;
ApplyArcherEffect(
target,
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
}
