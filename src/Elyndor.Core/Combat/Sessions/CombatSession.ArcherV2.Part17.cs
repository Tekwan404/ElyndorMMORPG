using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;
namespace Elyndor.Core.Combat.Sessions;
public sealed partial class CombatSession
{
private static bool IsPhysicalShotAbility(AbilityDefinition ability) =>
ability.Actions?.Any(action =>
action.Type == AbilityActionType.Damage
&& action.DamageType == DamageType.Physical) == true;
private void ConsumeArcherStackedEffect(
CombatActorState actor,
string effectId,
int count,
DateTimeOffset now)
{
ActiveEffect? effect = FindArcherEffect(actor, effectId, now);
if (effect is null)
return;
effect.Stacks = Math.Max(0, effect.Stacks - count);
if (effect.Stacks == 0)
RemoveArcherEffect(actor, effectId, now);
}
private sealed record ArcherAutoAttackModifier(
decimal DamageMultiplier,
decimal ArmorPenetrationBonus,
decimal AccuracyBonus,
decimal CriticalChanceBonus,
decimal CriticalDamageBonus,
bool Heavy,
bool Enchanted);
}
