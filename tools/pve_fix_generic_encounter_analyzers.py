from pathlib import Path

path = Path("src/Elyndor.Core/Combat/Sessions/CombatSession.GenericEncounter.cs")
text = path.read_text(encoding="utf-8")
replacements = [
    (
        """    private IReadOnlyDictionary<string, EncounterEnemyProfile> _genericEncounterEnemyProfiles =
        new Dictionary<string, EncounterEnemyProfile>(StringComparer.Ordinal);""",
        """    private Dictionary<string, EncounterEnemyProfile> _genericEncounterEnemyProfiles =
        new(StringComparer.Ordinal);""",
    ),
    (
        """    private IReadOnlyDictionary<string, EffectDefinition> _genericEncounterEffects =
        new Dictionary<string, EffectDefinition>(StringComparer.Ordinal);""",
        """    private Dictionary<string, EffectDefinition> _genericEncounterEffects =
        new(StringComparer.Ordinal);""",
    ),
]
for index, (old, new) in enumerate(replacements, start=1):
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"Patch fragment {index} expected exactly once, found {count}.")
    text = text.replace(old, new, 1)
path.write_text(text, encoding="utf-8")
