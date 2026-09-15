using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;
namespace Elyndor.Core.Combat.Sessions;
public sealed partial class CombatSession
{
private void ActivatePreparation(DateTimeOffset now)
{
_playerRuntime.Cooldowns.Remove("FREEZING_TRAP");
_playerRuntime.Cooldowns.Remove("IMMOLATION_TRAP");
_playerRuntime.Cooldowns.Remove("EXPLOSIVE_TRAP");
_survivalPreparationArmed =
TryGetArcherHook(
"S-9-1",
"SURVIVAL_MASTER_PREP",
out _);
}
private void OnTrapTriggered(
string trapAbilityId,
CombatActorState target,
DateTimeOffset now)
{
ApplyArcherEffect(
target,
new EffectDefinition(
TrapRecentEffectId,
EffectKind.Debuff,
TimeSpan.FromSeconds(5),
1,
EffectStackPolicy.Replace,
0,
SourceSpecific: true),
now);
if (TryGetArcherHook(
"S-2-2",
"TRAP_ENTRAPMENT",
out ResolvedTalentEventHook entrapment))
{
ApplyArcherEffect(
target,
new EffectDefinition(
TrapEntrapmentEffectId,
EffectKind.StatModifier,
entrapment.Duration,
1,
EffectStackPolicy.Replace,
1 - entrapment.Value / 100m,
ModifiedStat: EffectStat.AttackSpeed,
ModifierMode: EffectModifierMode.Multiplicative,
SourceSpecific: true),
now);
}
if (TryGetArcherHook(
"S-3-4",
"TRAP_FOCUS",
out ResolvedTalentEventHook focus)
&& ArcherTalentCooldownReady(focus.TalentId, now))
{
AddResource(_player.Actor, focus.Value, now, focus.TalentId);
StartArcherTalentCooldown(focus.TalentId, focus.InternalCooldown, now);
}
if (TryGetArcherHook(
"S-6-3",
"TRAP_CHAIN",
out ResolvedTalentEventHook chain)
&& ArcherTalentCooldownReady(chain.TalentId, now))
{
foreach (string other in new[] { "FREEZING_TRAP", "IMMOLATION_TRAP", "EXPLOSIVE_TRAP" })
{
if (!string.Equals(other, trapAbilityId, StringComparison.Ordinal))
ReduceArcherCooldown(other, TimeSpan.FromSeconds((double)chain.Value), now);
}
StartArcherTalentCooldown(chain.TalentId, chain.InternalCooldown, now);
}
if (TryGetArcherHook(
"S-5-2",
"TRAP_NEXT_SHOT",
out ResolvedTalentEventHook tactical))
{
ApplyOneShotBuff(
TrapNextShotEffectId,
tactical.SecondaryValue,
0,
tactical.Duration,
now);
ActiveEffect? effect =
FindArcherEffect(_player.Actor, TrapNextShotEffectId, now);
if (effect is not null)
effect.RemainingMagnitude = tactical.Value;
}
if (TryGetArcherHook(
"S-7-4",
"TRAP_ADAPTATION",
out ResolvedTalentEventHook adaptation))
{
ApplyArcherEffect(
_player.Actor,
new EffectDefinition(
AdaptationEffectId,
EffectKind.StatModifier,
adaptation.Duration,
1,
EffectStackPolicy.Replace,
Math.Max(0.1m, 1 - adaptation.Value / 100m),
ModifiedStat: EffectStat.IncomingDamageMultiplier,
ModifierMode: EffectModifierMode.Multiplicative,
SourceSpecific: true),
now);
}
if (TryGetArcherHook(
"S-8-2",
"MASTER_TACTICIAN",
out ResolvedTalentEventHook masterTactician))
{
ApplyOneShotBuff(
MasterTacticianEffectId,
masterTactician.Value,
masterTactician.SecondaryValue,
masterTactician.Duration,
now);
}
if (_survivalPreparationArmed
&& TryGetArcherHook(
"S-9-1",
"SURVIVAL_MASTER_PREP",
out ResolvedTalentEventHook master))
{
_survivalPreparationArmed = false;
AddResource(_player.Actor, master.Value, now, master.TalentId);
ApplyOneShotBuff(
SurvivalMasterShotEffectId,
master.SecondaryValue,
0,
TimeSpan.FromSeconds(5),
now);
}
}
}
