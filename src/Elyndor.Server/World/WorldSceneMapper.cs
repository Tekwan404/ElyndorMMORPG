using Elyndor.Contracts.World;
using Elyndor.Core.World;
using Elyndor.Infrastructure.World;
using Elyndor.Infrastructure.Professions;

namespace Elyndor.Server.World;

internal static class WorldSceneMapper
{
    public static WorldLocationSceneResponse ToResponse(WorldSceneSnapshot scene, IReadOnlyList<ResourceSceneState>? resources = null)
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
                DateTimeOffset? respawn = scene.EliteRespawns is not null
                    && scene.EliteRespawns.TryGetValue(encounter.MonsterId, out var deadline) ? deadline : null;
                if (respawn is not null && (nextChange is null || respawn < nextChange)) nextChange = respawn;
                objects.Add(new WorldSceneObjectResponse($"ENEMY_{resident.MonsterId}", "Enemy",
                    resident.DisplayName, resident.Description, 0, 0, resident,
                    encounter.Availability is not null, encounter.Availability is null ? null : change, RespawnAtUtc: respawn));
            }
        }

        foreach (var resource in resources ?? [])
        {
            var source = resource.Source;
            if (resource.AvailableAtUtc > scene.ServerTimeUtc && (nextChange is null || resource.AvailableAtUtc < nextChange))
                nextChange = resource.AvailableAtUtc;
            objects.Add(new WorldSceneObjectResponse($"RESOURCE_{source.Id}", "Resource", source.DisplayName,
                source.Description, 0, 0, Resource: new WorldResourceResponse(source.Id, source.ItemId,
                    resource.ItemName, resource.IconId, source.MinQuantity, source.MaxQuantity, source.RequiredSkill,
                    resource.LearnedSkill, resource.LearnedSkill >= source.RequiredSkill && resource.AvailableAtUtc <= scene.ServerTimeUtc,
                    resource.AvailableAtUtc)));
        }

        foreach (var point in location.Points ?? [])
            objects.Add(new WorldSceneObjectResponse($"POINT_{point.Id}", "Landmark",
                point.DisplayName, point.Description, 0, 0));

        // Existing quest definitions own NPC names, offers and game rules.
        foreach (var quest in (scene.Content.Package.Quests ?? [])
            .Where(quest => quest.OfferLocationId == location.Id && scene.CompletedQuestIds?.Contains(quest.Id) != true)
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
