using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;
namespace Elyndor.Core.Combat.Sessions;
public sealed partial class CombatSession
{
private const string HunterMarkEffectId = "ARCHER_HUNTER_MARK";
private const string SniperFocusEffectId = "ARCHER_SNIPER_FOCUS";
private const string ExposedDefenseEffectId = "ARCHER_EXPOSED_DEFENSE";
private const string DeadlyStreakEffectId = "ARCHER_DEADLY_STREAK";
private const string HeavyArrowEffectId = "ARCHER_HEAVY_ARROW";
private const string BrokenArmorEffectId = "ARCHER_BROKEN_ARMOR";
private const string HawkSpiritEffectId = "ARCHER_HAWK_SPIRIT";
private const string BeastHawkEffectId = "ARCHER_BM_HAWK";
private const string PetCritShotEffectId = "ARCHER_PET_CRIT_SHOT";
private const string CoordinationEffectId = "ARCHER_COORDINATION";
private const string BestialWrathPetDamageEffectId = "ARCHER_BESTIAL_WRATH_DAMAGE";
private const string BestialWrathPetAttackSpeedEffectId = "ARCHER_BESTIAL_WRATH_AS";
private const string FrenzyEffectId = "ARCHER_FRENZY";
private const string CommandVoiceEffectId = "ARCHER_COMMAND_VOICE";
private const string OwnerCritPetDamageEffectId = "ARCHER_OWNER_CRIT_PET_DAMAGE";
private const string BloodFangPetEffectId = "ARCHER_BLOOD_FANG_PET_NEXT";
private const string DeterrenceEffectId = "ARCHER_DETERRENCE";
private const string CounterShotEffectId = "ARCHER_DETERRENCE_COUNTER";
private const string TrapNextShotEffectId = "ARCHER_TRAP_NEXT_SHOT";
private const string AdaptationEffectId = "ARCHER_TRAP_ADAPTATION";
private const string MasterTacticianEffectId = "ARCHER_MASTER_TACTICIAN";
private const string SurvivalMasterShotEffectId = "ARCHER_SURVIVAL_MASTER_SHOT";
private const string TrapRecentEffectId = "ARCHER_TRAP_RECENT";
private const string SerpentStingEffectId = "ARCHER_SERPENT_STING";
private const string WyvernStingEffectId = "ARCHER_WYVERN_STING";
private const string WyvernWeaknessEffectId = "ARCHER_WYVERN_WEAKNESS";
private const string MendPetEffectId = "ARCHER_MEND_PET";
private const string TrapEntrapmentEffectId = "ARCHER_TRAP_ENTRAPMENT";
private const string TrueshotAuraEffectId = "ARCHER_TRUESHOT_AURA";
private const string ControlledReductionEffectId = "ARCHER_CONTROLLED_REDUCTION";
private const string LowHpReductionEffectId = "ARCHER_LOW_HP_REDUCTION";
private const string FreezingTrapStunEffectId = "ARCHER_FREEZING_TRAP_STUN";
private const string FreezingTrapBossEffectId = "ARCHER_FREEZING_TRAP_BOSS";
private const string IntimidationStunEffectId = "ARCHER_INTIMIDATION_STUN";
private const string IntimidationBossEffectId = "ARCHER_INTIMIDATION_BOSS";
private const string ShockingShotStunEffectId = "ARCHER_SHOCKING_SHOT_STUN";
private const string ImmolationTrapDotEffectId = "ARCHER_IMMOLATION_TRAP_DOT";
private const string ExplosiveTrapDotEffectId = "ARCHER_EXPLOSIVE_TRAP_DOT";
private int _archerShotSequence;
private int _markedShotSequence;
private Guid? _lastOwnerHitTargetId;
private DateTimeOffset? _lastOwnerHitAtUtc;
private Guid? _lastCompanionHitTargetId;
private DateTimeOffset? _lastCompanionHitAtUtc;
private DateTimeOffset? _nextSpiritBondAtUtc;
private bool _bestialWrathFirstCommandAvailable;
private bool _survivalPreparationArmed;
private bool IsArcher =>
string.Equals(_player.DefinitionId, "ARCHER", StringComparison.Ordinal);
private bool IsPhysicalCompanion => _companion is not null;
private string? CompanionArchetype => _companion?.DefinitionId switch
{
"ARCHER_STARTER_PREDATOR" => "PREDATOR",
"ARCHER_GUARDIAN" => "GUARDIAN",
"ARCHER_TRAPPER" => "TRAPPER",
_ => null
}
;
private AbilityDefinition ResolveArcherAbility(
AbilityDefinition ability,
DateTimeOffset now)
{
if (!IsArcher)
return ability;
decimal resourceCost = ability.ResourceCost;
decimal damageMultiplier = ability.DamageMultiplier;
decimal accuracyBonus = ability.AccuracyBonus;
decimal criticalChanceBonus = ability.CriticalChanceBonus;
TimeSpan castTime = ability.CastTime;
bool physicalShot = IsPhysicalShotAbility(ability);
if (physicalShot
&& TryGetArcherHook(
"M-1-1",
"PHYSICAL_SHOT_CRIT",
out ResolvedTalentEventHook lethalShooting))
{
criticalChanceBonus += lethalShooting.Value;
}
if (ability.ResourceCost > 0
&& !ability.IsSpell
&& TryGetArcherHook(
"M-1-2",
"PHYSICAL_FOCUS_COST",
out ResolvedTalentEventHook efficiency))
{
resourceCost *= Math.Max(0, 1 - efficiency.Value / 100m);
}
if (physicalShot && HasArcherEffect(_player.Actor, SniperFocusEffectId, now))
{
accuracyBonus += ArcherRuntimeParameter("SNIPER_FOCUS", "accuracyBonus");
criticalChanceBonus += ArcherRuntimeParameter("SNIPER_FOCUS", "criticalChanceBonus");
}
if (physicalShot
&& string.Equals(ability.Id, "AIMED_SHOT", StringComparison.Ordinal))
{
if (TryGetArcherHook(
"M-4-2",
"AIMED_SHOT_AIM",
out ResolvedTalentEventHook flawless))
{
accuracyBonus += flawless.Value;
criticalChanceBonus += flawless.SecondaryValue;
}
if (SelectedTargetHasHunterMark(now)
&& TryGetArcherHook(
"M-7-1",
"PERFECT_SHOT",
out ResolvedTalentEventHook perfect))
{
damageMultiplier *= 1 + perfect.Value / 100m;
castTime = ClampArcherCast(
castTime - TimeSpan.FromSeconds((double)perfect.CastTimeSeconds));
}
}
if (physicalShot)
{
ApplyOneShotModifier(
CoordinationEffectId,
ref damageMultiplier,
ref resourceCost,
now);
ApplyOneShotModifier(
PetCritShotEffectId,
ref damageMultiplier,
ref resourceCost,
now);
ApplyOneShotModifier(
CounterShotEffectId,
ref damageMultiplier,
ref resourceCost,
now);
ApplyOneShotModifier(
SurvivalMasterShotEffectId,
ref damageMultiplier,
ref resourceCost,
now);
ActiveEffect? tactical =
FindArcherEffect(_player.Actor, TrapNextShotEffectId, now);
if (tactical is not null)
{
damageMultiplier *= 1 + tactical.Definition.Magnitude / 100m;
criticalChanceBonus += tactical.RemainingMagnitude;
}
}
if (ability.ResourceCost > 0)
{
ActiveEffect? masterTactician =
FindArcherEffect(_player.Actor, MasterTacticianEffectId, now);
if (masterTactician is not null)
{
damageMultiplier *= 1 + masterTactician.Definition.Magnitude / 100m;
resourceCost *= Math.Max(
0,
1 - masterTactician.RemainingMagnitude / 100m);
}
}
return ability with
{
ResourceCost = Math.Max(0, resourceCost),
DamageMultiplier = Math.Max(0, damageMultiplier),
AccuracyBonus = accuracyBonus,
CriticalChanceBonus = criticalChanceBonus,
CastTime = castTime
};
}
private AbilityTargetModifier ResolveArcherTargetAbilityModifier(
AbilityDefinition ability,
CombatActorState target,
AbilityTargetModifier modifier,
DateTimeOffset now)
{
if (!IsArcher || target.IsDead)
return modifier;
decimal damageMultiplier = modifier.DamageMultiplier;
decimal accuracyBonus = modifier.AccuracyBonus;
decimal criticalChanceBonus = modifier.CriticalChanceBonus;
decimal criticalDamageBonus = modifier.CriticalDamageBonus;
decimal armorPenetrationBonus = modifier.ArmorPenetrationBonus;
decimal magicPenetrationBonus = modifier.MagicPenetrationBonus;
bool physicalShot = IsPhysicalShotAbility(ability);
bool marked = HasArcherEffect(target, HunterMarkEffectId, now);
if (physicalShot)
{
if (TryGetArcherHook(
"M-3-2",
"PHYSICAL_SHOT_CRIT_DAMAGE",
out ResolvedTalentEventHook deadlyShots))
{
criticalDamageBonus += deadlyShots.Value;
}
if (HasArcherEffect(_player.Actor, SniperFocusEffectId, now))
{
armorPenetrationBonus +=
ArcherRuntimeParameter("SNIPER_FOCUS", "armorPenetrationBonus");
}
if (TryGetArcherHook(
"M-5-2",
"BOW_DAMAGE",
out ResolvedTalentEventHook bowDamage))
{
damageMultiplier *= 1 + bowDamage.Value / 100m;
}
if (marked)
{
ActiveEffect mark = FindArcherEffect(target, HunterMarkEffectId, now)!;
damageMultiplier *= 1 + mark.Definition.Magnitude / 100m;
if (string.Equals(ability.Id, "AIMED_SHOT", StringComparison.Ordinal)
&& TryGetArcherHook(
"M-5-3",
"MARKED_VICTIM",
out ResolvedTalentEventHook victim))
{
damageMultiplier *= 1 + victim.SecondaryValue / 100m;
}
}
if (string.Equals(ability.Id, "AIMED_SHOT", StringComparison.Ordinal)
&& ArcherHpPercent(target) < 25
&& TryGetArcherHook(
"M-8-1",
"HEART_SHOT",
out ResolvedTalentEventHook heartShot))
{
damageMultiplier *= 1 + heartShot.Value / 100m;
}
ActiveEffect? exposed =
FindArcherEffect(_player.Actor, ExposedDefenseEffectId, now);
if (exposed is not null)
armorPenetrationBonus += exposed.Definition.Magnitude / 100m;
ActiveEffect? broken =
FindArcherEffect(target, BrokenArmorEffectId, now);
if (broken is not null)
armorPenetrationBonus += broken.Definition.Magnitude / 100m;
ActiveEffect? streak =
FindArcherEffect(_player.Actor, DeadlyStreakEffectId, now);
if (streak is not null)
criticalDamageBonus += streak.Definition.Magnitude * streak.Stacks;
bool poisoned = HasOwnedPoison(target, now);
if (poisoned
&& TryGetArcherHook(
"S-5-3",
"POISONED_TARGET_DAMAGE",
out ResolvedTalentEventHook poisonDamage))
{
damageMultiplier *= 1 + poisonDamage.Value / 100m;
}
if (poisoned
&& TryGetArcherHook(
"S-7-3",
"POISONED_TARGET_CRIT",
out ResolvedTalentEventHook poisonCrit))
{
criticalChanceBonus += poisonCrit.Value;
}
if ((poisoned || HasArcherEffect(target, TrapRecentEffectId, now))
&& TryGetArcherHook(
"S-9-1",
"SURVIVAL_MASTER_CRIT",
out ResolvedTalentEventHook masterCrit))
{
criticalChanceBonus += masterCrit.Value;
}
if (IsSharedTarget(target.ActorId, now)
&& TryGetArcherHook(
"B-3-2",
"JOINT_HUNT",
out ResolvedTalentEventHook joint))
{
damageMultiplier *= 1 + joint.Value / 100m;
}
}
return modifier with
{
DamageMultiplier = damageMultiplier,
AccuracyBonus = accuracyBonus,
CriticalChanceBonus = criticalChanceBonus,
CriticalDamageBonus = criticalDamageBonus,
ArmorPenetrationBonus = armorPenetrationBonus,
MagicPenetrationBonus = magicPenetrationBonus
};
}
private void OnArcherAbilityStarted(AbilityDefinition ability, DateTimeOffset now)
{
if (!IsArcher)
return;
bool physicalShot = IsPhysicalShotAbility(ability);
if (physicalShot)
{
ConsumeArcherStackedEffect(_player.Actor, ExposedDefenseEffectId, 1, now);
RemoveArcherEffect(_player.Actor, CoordinationEffectId, now);
RemoveArcherEffect(_player.Actor, PetCritShotEffectId, now);
RemoveArcherEffect(_player.Actor, CounterShotEffectId, now);
RemoveArcherEffect(_player.Actor, TrapNextShotEffectId, now);
RemoveArcherEffect(_player.Actor, SurvivalMasterShotEffectId, now);
}
if (ability.ResourceCost > 0)
RemoveArcherEffect(_player.Actor, MasterTacticianEffectId, now);
}
}
