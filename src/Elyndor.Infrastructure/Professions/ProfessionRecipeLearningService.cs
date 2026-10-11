using System.Data;
using System.Security.Cryptography;
using System.Text;
using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Professions;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Elyndor.Infrastructure.Professions;

public sealed class ProfessionRecipeLearningService(GameDbContext db, IContentSnapshotProvider contentProvider,
    ProfessionService professions, TimeProvider timeProvider)
{
    public async Task<ProfessionMutationResult> LearnAsync(Guid accountId, string recipeId, Guid mutationId, CancellationToken cancellationToken)
    {
        if (mutationId == Guid.Empty || string.IsNullOrWhiteSpace(recipeId)) return new(false, ProfessionErrorCodes.RecipeNotFound);
        return await db.Database.CreateExecutionStrategy().ExecuteInTransactionAsync(async _ =>
        {
            db.ChangeTracker.Clear();
            string lockKey = accountId.ToString("N");
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
            var character = await db.Characters.FromSqlInterpolated(
                $"SELECT * FROM game.characters WHERE \"AccountId\" = {accountId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
            if (character is null) return new ProfessionMutationResult(false, ProfessionErrorCodes.CharacterNotFound);
            string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"STUDY|{recipeId}")));
            var previous = await db.CharacterMutations.AsNoTracking().SingleOrDefaultAsync(
                mutation => mutation.CharacterId == character.Id && mutation.MutationId == mutationId, cancellationToken);
            if (previous is not null)
                return previous.OperationType == "PROFESSION_STUDY" && previous.RequestFingerprint == fingerprint
                    ? new ProfessionMutationResult(true, null, await professions.GetStateAsync(accountId, cancellationToken), Replayed: true)
                    : new ProfessionMutationResult(false, ProfessionErrorCodes.IdempotencyConflict);

            var package = contentProvider.GetCurrent().Package;
            var recipe = (package.ProfessionRecipes ?? []).SingleOrDefault(recipe => recipe.Id == recipeId);
            if (recipe?.LearningScrollItemId is null) return new ProfessionMutationResult(false, ProfessionErrorCodes.RecipeNotFound);
            var profession = await db.CharacterProfessions.SingleOrDefaultAsync(
                profession => profession.CharacterId == character.Id && profession.ProfessionId == recipe.ProfessionId, cancellationToken);
            if (profession is null) return new ProfessionMutationResult(false, ProfessionErrorCodes.ProfessionNotLearned);
            if (profession.Skill < recipe.RequiredSkill) return new ProfessionMutationResult(false, ProfessionErrorCodes.SkillTooLow);
            if (await db.CharacterRecipeKnowledge.AnyAsync(item => item.CharacterId == character.Id && item.ProfessionId == recipe.ProfessionId && item.RecipeId == recipeId, cancellationToken))
                return new ProfessionMutationResult(false, "profession_recipe_already_learned");
            DateTimeOffset now = timeProvider.GetUtcNow();
            if (await TravelPersistence.IsTravellingAsync(db, character.Id, now, cancellationToken))
                return new ProfessionMutationResult(false, "resource_travelling");
            string? location = await db.CharacterLocations.Where(item => item.CharacterId == character.Id)
                .Select(item => item.LocationId).SingleOrDefaultAsync(cancellationToken);
            if (location != recipe.RequiredLocationId) return new ProfessionMutationResult(false, ProfessionErrorCodes.WrongWorkshop);
            var scroll = await db.CharacterItems.Where(item => item.CharacterId == character.Id
                    && item.ItemDefinitionId == recipe.LearningScrollItemId && item.Storage == "INVENTORY" && !item.IsLocked && item.TransactionLockId == null
                    && !db.CharacterEquipment.Any(equipment => equipment.CharacterItemId == item.Id))
                .OrderBy(item => item.AcquiredAtUtc).ThenBy(item => item.Id).FirstOrDefaultAsync(cancellationToken);
            if (scroll is null) return new ProfessionMutationResult(false, "profession_recipe_scroll_missing");

            scroll.RemoveQuantity(1);
            if (scroll.Quantity == 0) db.CharacterItems.Remove(scroll);
            db.CharacterRecipeKnowledge.Add(new(character.Id, recipe.ProfessionId, recipe.Id, now));
            db.CharacterMutations.Add(new(character.Id, mutationId, "PROFESSION_STUDY", fingerprint, now));
            await db.SaveChangesAsync(cancellationToken);
            return new ProfessionMutationResult(true, null, await professions.GetStateAsync(accountId, cancellationToken));
        }, _ => Task.FromResult(false), IsolationLevel.Serializable, cancellationToken);
    }
}
