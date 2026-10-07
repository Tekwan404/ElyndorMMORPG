using System.Data;
using System.Reflection;
using System.Text.Json;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.Combat.SetPassives;
using Elyndor.Core.Combat.Damage;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Items;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.Characters;

public sealed class CharacterBuildSnapshotService(
    GameDbContext dbContext,
    CharacterDerivedStateService derivedStateService,
    IContentSnapshotProvider contentProvider,
    TimeProvider timeProvider)
{
    public Task<CharacterBuildSnapshot?> CaptureForTelegramAsync(long telegramId, CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.RepeatableRead, cancellationToken);
            var character = await (from candidate in dbContext.Characters.AsNoTracking()
                join account in dbContext.Accounts.AsNoTracking() on candidate.AccountId equals account.Id
                where account.TelegramUserId == telegramId
                select candidate).SingleOrDefaultAsync(cancellationToken);
            if (character is null) return null;
            var content = contentProvider.GetCurrent();
            var derived = await derivedStateService.ResolveAsync(character.Id, character.ClassId,
                character.Level, content, cancellationToken);
            var build = await CaptureAsync(character.Id, character.Name, character.ClassId,
                character.RaceId, character.GenderId, character.Level, derived, content, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return build;
        });

    public async Task<CharacterBuildSnapshot?> GetAsync(string buildHash, CancellationToken cancellationToken)
    {
        string? json = await dbContext.CharacterBuildArchives.AsNoTracking()
            .Where(build => build.BuildHash == buildHash).Select(build => build.PayloadJson)
            .SingleOrDefaultAsync(cancellationToken);
        return json is null ? null : JsonSerializer.Deserialize<CharacterBuildSnapshot>(json, CharacterBuildSnapshot.JsonOptions);
    }

    public async Task<CharacterBuildSnapshot?> GetTrainingAsync(Guid accountId, Guid sessionId, CancellationToken cancellationToken)
    {
        string? hash = await dbContext.TrainingBuildReferences.AsNoTracking()
            .Where(reference => reference.AccountId == accountId && reference.SessionId == sessionId)
            .Select(reference => reference.BuildHash).SingleOrDefaultAsync(cancellationToken);
        return hash is null ? null : await GetAsync(hash, cancellationToken);
    }

    public Task LinkTrainingAsync(Guid accountId, Guid sessionId, string buildHash, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO game.training_build_references ("SessionId", "AccountId", "BuildHash")
            VALUES ({sessionId}, {accountId}, {buildHash}) ON CONFLICT ("SessionId") DO NOTHING
            """, cancellationToken);

    public async Task<CharacterBuildSnapshot> CaptureAsync(
        Guid characterId, string name, string classId, string raceId, string genderId, int level,
        CharacterDerivedState derived, GameContentSnapshot content, CancellationToken cancellationToken)
    {
        Guid? artifactId = await dbContext.CharacterSpatialArtifacts.AsNoTracking()
            .Where(artifact => artifact.CharacterId == characterId)
            .Select(artifact => (Guid?)artifact.CharacterItemId).SingleOrDefaultAsync(cancellationToken);
        var equippedIds = derived.Inventory.Equipped.Values.Select(item => item.Id).ToList();
        if (artifactId.HasValue) equippedIds.Add(artifactId.Value);
        var instances = await dbContext.CharacterItems.AsNoTracking()
            .Where(item => item.CharacterId == characterId && equippedIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var reforges = await dbContext.ItemReforgeOperations.AsNoTracking()
            .Where(operation => operation.CharacterId == characterId
                && equippedIds.Contains(operation.ItemInstanceId)
                && operation.State == ItemReforgeOperationState.Accepted)
            .OrderBy(operation => operation.CreatedAtUtc).ThenBy(operation => operation.OperationId)
            .ToArrayAsync(cancellationToken);

        BuildEquipment MapItem(InventoryItemSnapshot item, string slot)
        {
            var instance = instances[item.Id];
            var baseDefinition = content.Indexes.ItemsById.GetValueOrDefault(item.Definition.Id) ?? item.Definition;
            var history = reforges.Where(operation => operation.ItemInstanceId == item.Id).Select(operation =>
            {
                var before = JsonSerializer.Deserialize<GeneratedItemInstance>(operation.CurrentItemJson)!;
                var after = JsonSerializer.Deserialize<GeneratedItemInstance>(operation.ProposedItemJson)!;
                return new BuildReforge(operation.SlotKey,
                    before.Affixes.SingleOrDefault(affix => affix.SlotKey == operation.SlotKey),
                    after.Affixes.SingleOrDefault(affix => affix.SlotKey == operation.SlotKey));
            }).ToArray();
            return new(slot, item.Id, instance.DefinitionVersion,
                instance.ItemLevel ?? item.Definition.RequiredLevel, instance.EnhancementLevel,
                baseDefinition, item.Definition with { Stats = item.EffectiveStats }, item.RolledPrimaryStats, item.GeneratedItem, history);
        }

        var equipment = derived.Inventory.Equipped.OrderBy(pair => pair.Key)
            .Select(pair => MapItem(pair.Value, pair.Key.ToString())).ToArray();
        var artifactItem = derived.Inventory.Items.SingleOrDefault(item => item.Id == artifactId);
        var setPieces = EquippedSetPieceCounter.Count(derived.Inventory);
        var sets = (content.Package.EquipmentSets ?? []).OrderBy(set => set.Id, StringComparer.Ordinal)
            .Select(set => new BuildSet(set, setPieces.GetValueOrDefault(set.Id),
                set.Bonuses.Where(bonus => setPieces.GetValueOrDefault(set.Id) >= bonus.RequiredPieces)
                    .OrderBy(bonus => bonus.RequiredPieces).ToArray(),
                SetPassiveCatalog.Definitions.Concat(EquipmentSetEffectResolver.Resolve([set])).Where(passive => passive.SetId == set.Id
                    && setPieces.GetValueOrDefault(set.Id) >= passive.RequiredPieces)
                    .OrderBy(passive => passive.Id, StringComparer.Ordinal).ToArray()))
            .Where(set => set.EquippedPieces > 0).ToArray();
        var talents = derived.ActiveTalentRanks.Where(pair => pair.Value > 0).OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair =>
            {
                var definition = derived.TalentTree!.Nodes.Single(talent => talent.Id == pair.Key);
                return new BuildTalent(definition, pair.Value,
                    derived.TalentTree.Branches.Single(branch => branch.Id == definition.BranchId).Name);
            }).ToArray();
        var stats = derived.StatCalculation.Breakdown.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair =>
            {
                decimal raw = pair.Value.Contributions.Count > 0
                    ? pair.Value.Contributions.Sum(contribution => contribution.Value) : pair.Value.FinalValue;
                if (pair.Key is "armorDamageReductionPercent" or "magicDamageReductionPercent")
                    raw = DefenseMitigationFormula.CalculateUncappedReductionPercent(
                        pair.Key == "armorDamageReductionPercent" ? derived.Stats.Armor : derived.Stats.MagicResistance, level);
                decimal effective = pair.Key is "armorPenetration" or "magicPenetration"
                    ? Math.Clamp(pair.Value.FinalValue, 0, 100) : pair.Value.FinalValue;
                string unit = pair.Key is "criticalChance" or "criticalDamage" or "accuracy" or "dodge"
                    or "blockChance" or "armorPenetration" or "magicPenetration"
                    or "armorDamageReductionPercent" or "magicDamageReductionPercent" ? "%"
                    : pair.Key == "attackSpeed" ? "×" : string.Empty;
                return new BuildStat(raw, effective, unit);
            }, StringComparer.Ordinal);
        var abilities = derived.KnownAbilityIds.Where(content.Indexes.AbilitiesById.ContainsKey)
            .Select(id => content.Indexes.AbilitiesById[id]).OrderBy(ability => ability.Id, StringComparer.Ordinal).ToArray();
        var modifiers = derived.TalentModifiers;
        var build = new CharacterBuildSnapshot(1, characterId, name, classId, raceId, genderId, level,
            content.ContentVersion, content.BalanceVersion,
            GameContentPackageCodec.ComputeSha256(GameContentPackageCodec.SerializeCanonical(content.Package)),
            typeof(CharacterBuildSnapshotService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown",
            stats, derived.EffectiveResourceProfile.MaxValue, derived.EffectiveResourceProfile.Id,
            derived.EffectiveResourceProfile.StartValue, derived.EffectiveResourceProfile.CombatRegenPerSecond,
            equipment, artifactItem is null ? null : MapItem(artifactItem, "SpatialArtifact"), sets, talents,
            abilities, modifiers.UnlockedAbilityIds.Order(StringComparer.Ordinal).ToArray(), derived.KnownAbilityIds.ToArray(),
            JsonSerializer.SerializeToElement(new { modifiers.Stats, modifiers.Combat, modifiers.Abilities,
                modifiers.EventHooks, modifiers.DeferredHooks, modifiers.Profiles,
                derived.ActiveCompanionProfile, derived.SelectedPhysicalCompanionProfile }),
            JsonSerializer.SerializeToElement(content.Package.StatFormula), JsonSerializer.SerializeToElement(derived.ClassProfile));
        string hash = build.BuildHash;
        string json = JsonSerializer.Serialize(build, CharacterBuildSnapshot.JsonOptions);
        DateTimeOffset capturedAt = timeProvider.GetUtcNow();
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO game.character_build_archives ("BuildHash", "PayloadJson", "CapturedAtUtc")
            VALUES ({hash}, CAST({json} AS jsonb), {capturedAt}) ON CONFLICT ("BuildHash") DO NOTHING
            """, cancellationToken);
        return build;
    }
}
