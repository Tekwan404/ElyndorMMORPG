using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;
namespace Elyndor.Core.Combat.Sessions;
public sealed partial class CombatSession
{
private void SyncSurvivalDefenses(DateTimeOffset now)
{
decimal controlReduction = 0;
if (TryGetArcherHook(
"S-1-2",
"CONTROL_DURATION_REDUCTION",
out ResolvedTalentEventHook sureFoot))
{
controlReduction += sureFoot.Value;
}
if (TryGetArcherHook(
"S-5-4",
"IRON_WILL",
out ResolvedTalentEventHook ironWill))
{
controlReduction += ironWill.Value;
bool controlled =
EffectEngine.HasControl(_player.Actor, EffectKind.Stun, now)
|| EffectEngine.HasControl(_player.Actor, EffectKind.Silence, now);
if (controlled)
{
EnsureArcherPlayerMultiplier(
ControlledReductionEffectId,
EffectStat.IncomingDamageMultiplier,
Math.Max(0.1m, 1 - ironWill.SecondaryValue / 100m),
now,
TimeSpan.FromSeconds(1));
}
else
{
RemoveArcherEffect(
_player.Actor,
ControlledReductionEffectId,
now);
}
}
_player.Actor.IncomingControlDurationMultiplier =
Math.Clamp(1 - controlReduction / 100m, 0.1m, 1m);
if (TryGetArcherHook(
"S-6-4",
"LOW_HP_REDUCTION",
out ResolvedTalentEventHook lowHp)
&& ArcherHpPercent(_player.Actor) < lowHp.Threshold)
{
EnsureArcherPlayerMultiplier(
LowHpReductionEffectId,
EffectStat.IncomingDamageMultiplier,
Math.Max(0.1m, 1 - lowHp.Value / 100m),
now,
TimeSpan.FromSeconds(1));
}
else
{
RemoveArcherEffect(
_player.Actor,
LowHpReductionEffectId,
now);
}
}
private void SyncTrueshotAura(DateTimeOffset now)
{
if (!TryGetArcherHook(
"M-6-1",
"TRUESHOT_AURA_GROUP",
out ResolvedTalentEventHook aura))
{
return;
}
foreach (CombatPlayerRuntimeState state in _playerStatesByActorId.Values)
{
if (state.Definition.Actor.ActorId == _player.Actor.ActorId)
continue;
if (state.Definition.DefinitionId is not ("WARRIOR" or "ARCHER"))
continue;
ApplyArcherEffectFrom(
state.Definition.Actor,
_player.Actor.ActorId,
new EffectDefinition(
TrueshotAuraEffectId,
EffectKind.StatModifier,
TimeSpan.FromSeconds(2),
1,
EffectStackPolicy.StrongestWins,
1 + aura.Value / 100m,
ModifiedStat: EffectStat.AttackPower,
ModifierMode: EffectModifierMode.Multiplicative,
SourceSpecific: false),
now);
}
}
private void SyncSpiritBond(DateTimeOffset now)
{
if (_companion is null
|| _companion.Actor.IsDead
|| !TryGetArcherHook(
"B-4-2",
"SPIRIT_BOND",
out ResolvedTalentEventHook bond))
{
_nextSpiritBondAtUtc = null;
return;
}
_nextSpiritBondAtUtc ??= now + TimeSpan.FromSeconds(10);
if (now < _nextSpiritBondAtUtc.Value)
return;
RestoreArcherHp(
_player.Actor,
_player.Actor.MaxHp * bond.Value / 100m,
now,
bond.TalentId);
RestoreArcherHp(
_companion.Actor,
_companion.Actor.MaxHp * bond.Value / 100m,
now,
bond.TalentId);
_nextSpiritBondAtUtc = now + TimeSpan.FromSeconds(10);
}
private void SyncBestialWrathControlImmunity(DateTimeOffset now)
{
if (_companion is null || _companion.Actor.IsDead)
return;
if (!HasArcherEffect(
_companion.Actor,
BestialWrathPetDamageEffectId,
now))
{
_bestialWrathFirstCommandAvailable = false;
return;
}
RemoveCompanionControls(now);
}
private decimal EffectiveArcherResourceRegenPerSecond(
decimal regen,
DateTimeOffset now) =>
regen;
}
