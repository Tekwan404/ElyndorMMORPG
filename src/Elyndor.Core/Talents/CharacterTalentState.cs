using System.Text.Json;

namespace Elyndor.Core.Talents;

public sealed class CharacterTalentState
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private CharacterTalentState()
    {
        TalentTreeId = null!;
        ActiveLoadoutId = null!;
        Loadout1RanksJson = null!;
        Loadout2RanksJson = null!;
    }

    public CharacterTalentState(
        Guid characterId,
        string talentTreeId,
        int talentVersion,
        DateTimeOffset changedAtUtc)
    {
        if (characterId == Guid.Empty) throw new ArgumentException("Character id cannot be empty.", nameof(characterId));
        ArgumentException.ThrowIfNullOrWhiteSpace(talentTreeId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(talentVersion);
        EnsureUtc(changedAtUtc);

        CharacterId = characterId;
        TalentTreeId = talentTreeId;
        ActiveLoadoutId = TalentLoadoutIds.Loadout1;
        Loadout1RanksJson = "{}";
        Loadout2RanksJson = "{}";
        TalentVersion = talentVersion;
        StateVersion = 1;
        LastChangedAtUtc = changedAtUtc;
    }

    public Guid CharacterId { get; private set; }
    public string TalentTreeId { get; private set; }
    public string ActiveLoadoutId { get; private set; }
    public string Loadout1RanksJson { get; private set; }
    public string Loadout2RanksJson { get; private set; }
    public int TalentVersion { get; private set; }
    public long StateVersion { get; private set; }
    public DateTimeOffset LastChangedAtUtc { get; private set; }

    public IReadOnlyDictionary<string, int> GetRanks(string loadoutId) =>
        Deserialize(GetJson(loadoutId));

    public void ReplaceRanks(
        string loadoutId,
        IReadOnlyDictionary<string, int> selectedRanks,
        DateTimeOffset changedAtUtc)
    {
        EnsureUtc(changedAtUtc);
        string json = JsonSerializer.Serialize(
            selectedRanks.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            JsonOptions);
        if (loadoutId == TalentLoadoutIds.Loadout1) Loadout1RanksJson = json;
        else if (loadoutId == TalentLoadoutIds.Loadout2) Loadout2RanksJson = json;
        else throw new ArgumentOutOfRangeException(nameof(loadoutId));
        Touch(changedAtUtc);
    }

    public void SwitchLoadout(string loadoutId, DateTimeOffset changedAtUtc)
    {
        EnsureUtc(changedAtUtc);
        if (!TalentLoadoutIds.IsValid(loadoutId)) throw new ArgumentOutOfRangeException(nameof(loadoutId));
        ActiveLoadoutId = loadoutId;
        Touch(changedAtUtc);
    }

    public void Reset(string loadoutId, DateTimeOffset changedAtUtc) =>
        ReplaceRanks(loadoutId, new Dictionary<string, int>(), changedAtUtc);

    private string GetJson(string loadoutId) => loadoutId switch
    {
        TalentLoadoutIds.Loadout1 => Loadout1RanksJson,
        TalentLoadoutIds.Loadout2 => Loadout2RanksJson,
        _ => throw new ArgumentOutOfRangeException(nameof(loadoutId))
    };

    private static Dictionary<string, int> Deserialize(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, int>>(json, JsonOptions)
        ?? new Dictionary<string, int>(StringComparer.Ordinal);

    private void Touch(DateTimeOffset changedAtUtc)
    {
        StateVersion++;
        LastChangedAtUtc = changedAtUtc;
    }

    private static void EnsureUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero) throw new ArgumentException("Talent timestamps must be UTC.");
    }
}
