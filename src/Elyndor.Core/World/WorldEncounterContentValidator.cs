using Elyndor.Core.Content;
using Elyndor.Core.Monsters;

namespace Elyndor.Core.World;

public static class WorldEncounterContentValidator
{
    public static IReadOnlyList<ContentValidationError> Validate(GameContentPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        List<ContentValidationError> errors = [];
        Dictionary<string, MonsterDefinition> monsters = (package.Monsters ?? [])
            .GroupBy(monster => monster.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        for (var locationIndex = 0; locationIndex < package.Locations.Count; locationIndex++)
        {
            LocationDefinition location = package.Locations[locationIndex];
            IReadOnlyList<LocationEncounterDefinition> encounters = location.Encounters ?? [];
            bool isSafe = string.Equals(location.DangerLevel, "SAFE", StringComparison.Ordinal);
            if (!isSafe && encounters.Count == 0)
            {
                errors.Add(new(
                    "HOSTILE_LOCATION_HAS_NO_ENCOUNTERS",
                    $"locations[{locationIndex}].encounters",
                    $"Non-safe location '{location.Id}' must define at least one ordinary encounter."));
                continue;
            }

            if (encounters.Count == 0) continue;

            if (isSafe)
            {
                errors.Add(new(
                    "SAFE_LOCATION_HAS_HOSTILE_ENCOUNTERS",
                    $"locations[{locationIndex}].encounters",
                    $"Safe location '{location.Id}' cannot define ordinary hostile encounters."));
            }

            HashSet<string> encounteredMonsterIds = new(StringComparer.Ordinal);
            for (var encounterIndex = 0; encounterIndex < encounters.Count; encounterIndex++)
            {
                LocationEncounterDefinition encounter = encounters[encounterIndex];
                string path = $"locations[{locationIndex}].encounters[{encounterIndex}]";
                if (string.IsNullOrWhiteSpace(encounter.MonsterId)
                    || encounter.Weight <= 0
                    || !encounteredMonsterIds.Add(encounter.MonsterId))
                {
                    errors.Add(new(
                        "INVALID_LOCATION_ENCOUNTER",
                        path,
                        $"Location '{location.Id}' contains an invalid or duplicate encounter entry."));
                    continue;
                }

                if (!monsters.TryGetValue(encounter.MonsterId, out MonsterDefinition? monster))
                {
                    errors.Add(new(
                        "MISSING_LOCATION_ENCOUNTER_MONSTER",
                        path,
                        $"Location '{location.Id}' references missing monster '{encounter.MonsterId}'."));
                    continue;
                }

                if (monster.Rank != MonsterRank.Normal)
                {
                    errors.Add(new(
                        "INVALID_LOCATION_ENCOUNTER_RANK",
                        path,
                        $"Ordinary location encounter '{monster.Id}' must use Normal monster rank."));
                }

                if (string.IsNullOrWhiteSpace(monster.DisplayName)
                    || string.IsNullOrWhiteSpace(monster.Description)
                    || string.IsNullOrWhiteSpace(monster.ArtId))
                {
                    errors.Add(new(
                        "MISSING_ENCOUNTER_PRESENTATION",
                        path,
                        $"Encounter monster '{monster.Id}' must define displayName, description, and artId."));
                }
            }
        }

        return errors;
    }
}
