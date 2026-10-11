using System.Data;
using System.Security.Cryptography;
using System.Text;
using Elyndor.Core.Characters;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Content;
using Elyndor.Core.Professions;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Elyndor.Infrastructure.Professions;

public sealed record ResourceSceneState(ResourceSourceDefinition Source, string ItemName,
    string? IconId, int LearnedSkill, DateTimeOffset AvailableAtUtc);

public sealed class ResourceGatheringService(GameDbContext dbContext, IContentSnapshotProvider contentProvider,
    ProfessionService professionService, IGameRandomFactory randomFactory, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<ResourceSceneState>> GetSceneAsync(Guid characterId, string locationId, CancellationToken cancellationToken)
    {
        var content = contentProvider.GetCurrent();
        var sources = (content.Package.ResourceSources ?? []).Where(source => source.LocationId == locationId).ToArray();
        var ids = sources.Select(source => source.Id).ToArray();
        var states = await dbContext.ResourceNodes.AsNoTracking().Where(node => ids.Contains(node.NodeId))
            .ToDictionaryAsync(node => node.NodeId, cancellationToken);
        var skills = await dbContext.CharacterProfessions.AsNoTracking().Where(profession => profession.CharacterId == characterId)
            .ToDictionaryAsync(profession => profession.ProfessionId, profession => profession.Skill, cancellationToken);
        return sources.Select(source => new ResourceSceneState(source, content.Indexes.ItemsById[source.ItemId].Name,
            content.Indexes.ItemsById[source.ItemId].IconId, skills.GetValueOrDefault(source.ProfessionId),
            states.GetValueOrDefault(source.Id)?.AvailableAtUtc ?? DateTimeOffset.UnixEpoch)).ToArray();
    }

    public async Task<ProfessionMutationResult> GatherAsync(Guid accountId, string nodeId, Guid mutationId, CancellationToken cancellationToken)
    {
        if (mutationId == Guid.Empty || string.IsNullOrWhiteSpace(nodeId))
            return new(false, "resource_invalid_request");
        return await dbContext.Database.CreateExecutionStrategy().ExecuteInTransactionAsync(async _ =>
        {
            dbContext.ChangeTracker.Clear();
            var character = await dbContext.Characters.FromSqlInterpolated(
                $"SELECT * FROM game.characters WHERE \"AccountId\" = {accountId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
            if (character is null) return new ProfessionMutationResult(false, ProfessionErrorCodes.CharacterNotFound);
            string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"GATHER|{nodeId}")));
            var previous = await dbContext.CharacterMutations.AsNoTracking().SingleOrDefaultAsync(
                mutation => mutation.CharacterId == character.Id && mutation.MutationId == mutationId, cancellationToken);
            if (previous is not null)
            {
                if (previous.OperationType != "RESOURCE_GATHER" || previous.RequestFingerprint != fingerprint)
                    return new ProfessionMutationResult(false, ProfessionErrorCodes.IdempotencyConflict);
                var receipt = await dbContext.ResourceGatheringReceipts.AsNoTracking().SingleAsync(
                    receipt => receipt.CharacterId == character.Id && receipt.MutationId == mutationId, cancellationToken);
                return new ProfessionMutationResult(true, null, ItemId: receipt.ItemId, Quantity: receipt.Quantity,
                    SkillIncreased: receipt.SkillIncreased, Replayed: true);
            }
            var content = contentProvider.GetCurrent();
            var source = (content.Package.ResourceSources ?? []).SingleOrDefault(source => source.Id == nodeId);
            if (source is null) return new ProfessionMutationResult(false, "resource_not_found");
            var profession = await dbContext.CharacterProfessions.SingleOrDefaultAsync(
                profession => profession.CharacterId == character.Id && profession.ProfessionId == source.ProfessionId, cancellationToken);
            if (profession is null) return new ProfessionMutationResult(false, ProfessionErrorCodes.ProfessionNotLearned);
            if (profession.Skill < source.RequiredSkill) return new ProfessionMutationResult(false, ProfessionErrorCodes.SkillTooLow);
            DateTimeOffset now = timeProvider.GetUtcNow();
            if (await TravelPersistence.IsTravellingAsync(dbContext, character.Id, now, cancellationToken))
                return new ProfessionMutationResult(false, "resource_travelling");
            string? locationId = await dbContext.CharacterLocations.Where(location => location.CharacterId == character.Id)
                .Select(location => location.LocationId).SingleOrDefaultAsync(cancellationToken);
            if (locationId != source.LocationId) return new ProfessionMutationResult(false, "resource_wrong_location");
            if (await dbContext.AfkFarmSessions.AnyAsync(farm => farm.CharacterId == character.Id && farm.Status == Elyndor.Core.Afk.AfkFarmStatus.Active, cancellationToken))
                return new ProfessionMutationResult(false, "resource_afk_active");
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO game.resource_nodes (node_id, available_at_utc) VALUES ({nodeId}, {DateTimeOffset.UnixEpoch}) ON CONFLICT (node_id) DO NOTHING", cancellationToken);
            var node = await dbContext.ResourceNodes.FromSqlInterpolated(
                $"SELECT * FROM game.resource_nodes WHERE node_id = {nodeId} FOR UPDATE").SingleAsync(cancellationToken);
            if (node.AvailableAtUtc > now) return new ProfessionMutationResult(false, "resource_unavailable");
            var random = randomFactory.Create();
            int quantity = source.MinQuantity + (int)(random.NextUnit() * (source.MaxQuantity - source.MinQuantity + 1));
            var failure = await professionService.AddStackableAsync(character.Id, content.Indexes.ItemsById[source.ItemId], quantity, now, content, cancellationToken);
            if (failure is not null) return failure;
            int maxSkill = content.Package.Professions!.Single(definition => definition.Id == source.ProfessionId).MaxSkill;
            bool increased = ProfessionSkillRules.ShouldIncrease(random, profession.Skill, source.RequiredSkill, source.SkillUpUntil)
                && profession.TryIncreaseSkill(maxSkill, now);
            if (!node.TryHarvest(now, source.RespawnSeconds)) throw new InvalidOperationException("Locked resource became unavailable.");
            dbContext.ResourceGatheringReceipts.Add(new(character.Id, mutationId, nodeId, source.ItemId, quantity, increased, now));
            dbContext.CharacterMutations.Add(new(character.Id, mutationId, "RESOURCE_GATHER", fingerprint, now));
            await dbContext.SaveChangesAsync(cancellationToken);
            return new ProfessionMutationResult(true, null, ItemId: source.ItemId, Quantity: quantity, SkillIncreased: increased);
        }, _ => Task.FromResult(false), IsolationLevel.Serializable, cancellationToken);
    }
}
