using System.Data;
using Elyndor.Core.Characters;
using Elyndor.Core.Items;
using Elyndor.Core.Professions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Elyndor.Infrastructure.Professions;

public sealed partial class ProfessionService
{
    public Task<ProfessionMutationResult> GatherAsync(Guid accountId, string nodeId, Guid mutationId, CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteInTransactionAsync(async _ =>
        {
            dbContext.ChangeTracker.Clear();
            var character = await dbContext.Characters.SingleOrDefaultAsync(item => item.AccountId == accountId, cancellationToken);
            if (character is null) return new(false, ProfessionErrorCodes.CharacterNotFound);
            var content = contentProvider.GetCurrent();
            var node = content.Package.ProfessionGatheringNodes?.SingleOrDefault(item => item.Id == nodeId);
            if (node is null || mutationId == Guid.Empty) return new(false, "profession_node_not_found");
            string fingerprint = Fingerprint($"GATHER|{nodeId}");
            var replay = await ReplayAsync(character.Id, mutationId, "PROFESSION_GATHER", fingerprint, cancellationToken);
            if (replay is not null) return replay;
            var profession = await dbContext.CharacterProfessions.SingleOrDefaultAsync(
                item => item.CharacterId == character.Id && item.ProfessionId == ProfessionIds.Mining, cancellationToken);
            if (profession is null) return new(false, ProfessionErrorCodes.ProfessionNotLearned);
            if (profession.Skill < node.RequiredSkill) return new(false, ProfessionErrorCodes.SkillTooLow);
            if (!await IsAtLocationAsync(character.Id, node.LocationId, cancellationToken))
                return new(false, "profession_wrong_location");
            var now = timeProvider.GetUtcNow();
            if (await MiningCooldownUntilAsync(character.Id, cancellationToken) > now) return new(false, "profession_gathering_cooldown");
            var definition = content.Package.Items!.Single(item => item.Id == node.ItemId);
            var failure = await AddStackableAsync(character.Id, definition, node.Quantity, now, content, cancellationToken);
            if (failure is not null) return failure;
            bool increased = ShouldIncreaseProfessionSkill(profession.Skill, node.RequiredSkill, node.SkillUpUntil)
                && profession.TryIncreaseSkill(300, now);
            profession.CompleteGathering(now, node.CooldownSeconds);
            dbContext.CharacterMutations.Add(new(character.Id, mutationId, "PROFESSION_GATHER", fingerprint, now));
            await dbContext.SaveChangesAsync(cancellationToken);
            return new(true, null, ItemId: node.ItemId, Quantity: node.Quantity, SkillIncreased: increased);
        }, _ => Task.FromResult(false), IsolationLevel.Serializable, cancellationToken);

    public Task<ProfessionMutationResult> DisenchantAsync(Guid accountId, Guid itemId, Guid mutationId, CancellationToken cancellationToken) =>
        MutateEnchantmentAsync(accountId, itemId, null, mutationId, cancellationToken);

    public Task<ProfessionMutationResult> EnchantAsync(Guid accountId, Guid itemId, string enchantmentId, Guid mutationId, CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(enchantmentId)
            ? Task.FromResult(new ProfessionMutationResult(false, "profession_enchantment_not_found"))
            : MutateEnchantmentAsync(accountId, itemId, enchantmentId, mutationId, cancellationToken);

    private Task<ProfessionMutationResult> MutateEnchantmentAsync(Guid accountId, Guid itemId, string? enchantmentId, Guid mutationId, CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteInTransactionAsync(async _ =>
        {
            dbContext.ChangeTracker.Clear();
            var character = await dbContext.Characters.SingleOrDefaultAsync(item => item.AccountId == accountId, cancellationToken);
            if (character is null) return new(false, ProfessionErrorCodes.CharacterNotFound);
            if (mutationId == Guid.Empty || itemId == Guid.Empty) return new(false, "profession_item_not_found");
            string operation = enchantmentId is null ? "PROFESSION_DISENCHANT" : "PROFESSION_ENCHANT";
            string fingerprint = Fingerprint($"{operation}|{itemId:N}|{enchantmentId}");
            var replay = await ReplayAsync(character.Id, mutationId, operation, fingerprint, cancellationToken);
            if (replay is not null) return replay;
            var profession = await dbContext.CharacterProfessions.SingleOrDefaultAsync(
                item => item.CharacterId == character.Id && item.ProfessionId == ProfessionIds.Enchanting, cancellationToken);
            if (profession is null) return new(false, ProfessionErrorCodes.ProfessionNotLearned);
            if (!await IsAtLocationAsync(character.Id, "STARTER_TOWN", cancellationToken))
                return new(false, ProfessionErrorCodes.WrongWorkshop);
            var target = await dbContext.CharacterItems.SingleOrDefaultAsync(
                item => item.Id == itemId && item.CharacterId == character.Id, cancellationToken);
            if (target is null) return new(false, "profession_item_not_found");
            if (target.IsLocked || target.TransactionLockId.HasValue || await dbContext.CharacterEquipment.AnyAsync(
                    item => item.CharacterId == character.Id && item.CharacterItemId == itemId, cancellationToken))
                return new(false, "profession_item_protected");
            var content = contentProvider.GetCurrent();
            var definition = content.Package.Items?.SingleOrDefault(item => item.Id == target.ItemDefinitionId);
            if (definition?.Type != ItemType.Equipment) return new(false, "profession_invalid_equipment");
            var now = timeProvider.GetUtcNow();
            string? outputId = null;
            int quantity = 0;
            if (enchantmentId is null)
            {
                var rule = content.Package.ProfessionDisenchanting?.SingleOrDefault(item => item.Rarity == definition.Rarity);
                if (rule is null) return new(false, "profession_invalid_equipment");
                var dust = content.Package.Items!.Single(item => item.Id == rule.ItemId);
                var failure = await AddStackableAsync(character.Id, dust, rule.Quantity, now, content, cancellationToken, releasedSlots: 1);
                if (failure is not null) return failure;
                dbContext.CharacterItems.Remove(target);
                outputId = rule.ItemId;
                quantity = rule.Quantity;
            }
            else
            {
                var enchantment = content.Package.ProfessionEnchantments?.SingleOrDefault(item => item.Id == enchantmentId);
                if (enchantment is null || !definition.Slot.HasValue || !enchantment.Slots.Contains(definition.Slot.Value))
                    return new(false, "profession_enchantment_slot");
                var scroll = await dbContext.CharacterItems.Where(item => item.CharacterId == character.Id
                        && item.ItemDefinitionId == enchantment.ScrollItemId && !item.IsLocked && item.TransactionLockId == null)
                    .OrderBy(item => item.AcquiredAtUtc).FirstOrDefaultAsync(cancellationToken);
                if (scroll is null) return new(false, ProfessionErrorCodes.MissingIngredients);
                if (scroll.Quantity == 1) dbContext.CharacterItems.Remove(scroll);
                else scroll.RemoveQuantity(1);
                target.SetEnchantment(enchantment.Id, enchantment.StatId, enchantment.Value);
            }
            dbContext.CharacterMutations.Add(new(character.Id, mutationId, operation, fingerprint, now));
            await dbContext.SaveChangesAsync(cancellationToken);
            return new(true, null, ItemId: outputId, Quantity: quantity);
        }, _ => Task.FromResult(false), IsolationLevel.Serializable, cancellationToken);

    private async Task<DateTimeOffset?> MiningCooldownUntilAsync(Guid characterId, CancellationToken cancellationToken)
    {
        int cooldown = (contentProvider.GetCurrent().Package.ProfessionGatheringNodes ?? []).Select(node => node.CooldownSeconds).DefaultIfEmpty(0).Max();
        var cutoff = timeProvider.GetUtcNow().AddSeconds(-cooldown);
        var last = await dbContext.CharacterMutations.AsNoTracking()
            .Where(item => item.CharacterId == characterId && item.OperationType == "PROFESSION_GATHER" && item.CommittedAtUtc > cutoff)
            .OrderByDescending(item => item.CommittedAtUtc).Select(item => (DateTimeOffset?)item.CommittedAtUtc).FirstOrDefaultAsync(cancellationToken);
        return last?.AddSeconds(cooldown);
    }

    private bool ShouldIncreaseProfessionSkill(int currentSkill, int requiredSkill, int skillUpUntil) =>
        ProfessionSkillRules.ShouldIncrease(professionRandom, currentSkill, requiredSkill, skillUpUntil);

    private async Task<bool> IsAtLocationAsync(Guid characterId, string locationId, CancellationToken cancellationToken) =>
        await dbContext.CharacterLocations.AnyAsync(item => item.CharacterId == characterId && item.LocationId == locationId, cancellationToken)
        && !await dbContext.CharacterTravelStates.AnyAsync(item => item.CharacterId == characterId, cancellationToken);

    private async Task<ProfessionMutationResult?> ReplayAsync(Guid characterId, Guid mutationId, string operation, string fingerprint, CancellationToken cancellationToken)
    {
        CharacterMutation? previous = await dbContext.CharacterMutations.AsNoTracking().SingleOrDefaultAsync(
            item => item.CharacterId == characterId && item.MutationId == mutationId, cancellationToken);
        if (previous is null) return null;
        return previous.OperationType == operation && previous.RequestFingerprint == fingerprint
            ? new(true, null, Replayed: true)
            : new(false, ProfessionErrorCodes.IdempotencyConflict);
    }
}
