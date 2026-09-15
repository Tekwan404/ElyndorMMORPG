using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;
namespace Elyndor.Core.Combat.Sessions;
public sealed partial class CombatSession
{
private bool ArcherTalentCooldownReady(
string key,
DateTimeOffset now) =>
!_talentInternalCooldowns.TryGetValue(key, out DateTimeOffset readyAt)
|| readyAt <= now;
private void StartArcherTalentCooldown(
string key,
TimeSpan duration,
DateTimeOffset now)
{
if (duration > TimeSpan.Zero)
_talentInternalCooldowns[key] = now + duration;
}
private static decimal ArcherHpPercent(CombatActorState actor) =>
actor.MaxHp <= 0
? 0
: actor.CurrentHp / actor.MaxHp * 100m;
private static TimeSpan ClampArcherCast(TimeSpan castTime) =>
castTime < TimeSpan.FromMilliseconds(100)
? TimeSpan.FromMilliseconds(100)
: castTime;
private static bool IsPhysicalShotDefinition(string? definitionId) =>
definitionId is
"AUTO_ATTACK"
or "SHOCKING_SHOT"
or "PIERCING_ARROW"
or "AIMED_SHOT"
or "MULTI_SHOT"
or "DISORIENTING_SHOT"
or "VOLLEY";
}
