using Elyndor.Core.Characters;

namespace Elyndor.Core.Afk;

/// <summary>
/// Immutable character inputs captured when an AFK session starts. Opaque JSON payloads keep
/// the exact equipment/resource/talent definitions used by the current combat stack without
/// making later processing read mutable live character state.
/// </summary>
public sealed record AfkCharacterSnapshot(
    string ClassId,
    int Level,
    CharacterStats Stats,
    decimal CurrentHp,
    decimal CurrentResource,
    string ClassProfileJson,
    string ResourceProfileJson,
    string EquipmentJson,
    IReadOnlyDictionary<string, int> ActiveTalentRanks,
    string TalentModifiersJson,
    IReadOnlyList<string> KnownAbilityIds);
