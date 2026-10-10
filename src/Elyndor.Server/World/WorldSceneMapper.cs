using Elyndor.Contracts.World;
using Elyndor.Core.World;
using Elyndor.Infrastructure.World;

namespace Elyndor.Server.World;

internal static class WorldSceneMapper
{
    public static WorldLocationSceneResponse ToResponse(WorldSceneSnapshot scene)
    {
        var location = scene.Location;
        var objects = new List<WorldSceneObjectResponse>();
        bool isDungeon = scene.Content.Indexes.DungeonsById.Values.Any(dungeon =>
            dungeon.Id == location.Id || dungeon.EntryLocationId == location.Id);
        var residents = WorldEndpoints.BuildResidents(location, scene.Content)
            .ToDictionary(resident => resident.MonsterId, StringComparer.Ordinal);
        DateTimeOffset? nextChange = null;

        if (!isDungeon && location.DangerLevel != "SAFE")
        {
            foreach (var encounter in location.Encounters ?? [])
            {
                DateTimeOffset? change = LocationEncounterAvailability.NextChange(encounter, scene.ServerTimeUtc);
                if (change is not null && (nextChange is null || change < nextChange))
                    nextChange = change;
                if (!LocationEncounterAvailability.IsAvailable(encounter, scene.ServerTimeUtc)
                    || !residents.TryGetValue(encounter.MonsterId, out var resident))
                    continue;
                objects.Add(new WorldSceneObjectResponse($"ENEMY_{resident.MonsterId}", "Enemy",
                    resident.DisplayName, resident.Description, 0, 0, resident,
                    encounter.Availability is not null, encounter.Availability is null ? null : change));
            }
        }

        foreach (var point in location.Points ?? [])
            objects.Add(new WorldSceneObjectResponse($"POINT_{point.Id}", "Landmark",
                point.DisplayName, point.Description, 0, 0));

        // Existing quest definitions own NPC names, offers and game rules.
        foreach (var quest in (scene.Content.Package.Quests ?? [])
            .Where(quest => quest.OfferLocationId == location.Id)
            .OrderBy(quest => quest.RequiredLevel).ThenBy(quest => quest.Id, StringComparer.Ordinal))
            objects.Add(new WorldSceneObjectResponse($"QUEST_{quest.Id}", "Npc",
                quest.IssuerName ?? quest.DisplayName, quest.Description, 0, 0, QuestId: quest.Id));

        // Eight presentation slots per page; adding content never creates an endless scene.
        var positioned = objects.Select((entry, index) => entry with
        {
            X = 18 + index % 8 % 3 * 32,
            Y = 30 + index % 8 / 3 * 23
        }).ToArray();
        return new WorldLocationSceneResponse(location.Id, scene.Content.ContentVersion,
            LocationWorldState.Calm.ToString(), scene.ServerTimeUtc, nextChange, positioned);
    }
}
