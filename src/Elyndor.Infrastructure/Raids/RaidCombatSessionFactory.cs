using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Participants;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.World;

namespace Elyndor.Infrastructure.Raids;

public sealed class RaidCombatSessionFactory(
    BootstrapService bootstrapService,
    CharacterDerivedStateService derivedStateService,
    IContentSnapshotProvider contentProvider,
    IGameRandomFactory randomFactory,
    TimeProvider timeProvider,
    CharacterAbilityCooldownStore cooldownStore)
{
    private static readonly HashSet<string> PlayableCombatClassIds = new(StringComparer.Ordinal)
    {
        "WARRIOR",
        "ARCHER",
        "MAGE",
        "PALADIN"
    };

    public async Task<CombatSessionCreationResult> CreateAsync(
        Guid leaderAccountId,
        string monsterId,
        string expectedLocationId,
        IReadOnlyList<RaidCombatMember> capturedRoster,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capturedRoster);
        if (capturedRoster.Count == 0
            || capturedRoster.Count > CombatParticipantLimit.MaximumRaid)
        {
            return Failure(CombatErrorCodes.InvalidLocation);
        }

        RaidCombatMember? leaderMember = capturedRoster.SingleOrDefault(member => member.IsLeader);
        if (leaderMember is null || leaderMember.AccountId != leaderAccountId)
            return Failure(CombatErrorCodes.CommandRejected);

        GameContentSnapshot contentSnapshot = contentProvider.GetCurrent();
        GameContentPackage content = contentSnapshot.Package;
        GameContentIndexes indexes = contentSnapshot.Indexes;

        BootstrapSnapshot leaderBootstrap = await bootstrapService.GetAsync(
            leaderAccountId,
            contentSnapshot,
            cancellationToken,
            checkpoint: true);
        BootstrapCharacter? character = leaderBootstrap.Character;
        if (character is null || character.Id != leaderMember.CharacterId)
            return Failure("character_not_found");
        if (leaderBootstrap.AfkFarm?.Status == Elyndor.Core.Afk.AfkFarmStatus.Active)
            return Failure(CombatErrorCodes.AfkFarmActive, character.Id);
        if (!PlayableCombatClassIds.Contains(character.ClassId))
            return Failure(CombatErrorCodes.UnsupportedClass, character.Id);
        if (character.Vitals.CurrentHp <= 0
            || leaderBootstrap.World is null
            || leaderBootstrap.World.Travel is not null
            || !string.Equals(
                leaderBootstrap.World.CurrentLocation.Id,
                expectedLocationId,
                StringComparison.Ordinal))
        {
            return Failure(CombatErrorCodes.InvalidLocation, character.Id);
        }

        MonsterDefinition? monster = indexes.MonstersById.GetValueOrDefault(monsterId);
        if (monster is null
            || monster.Rank is not (MonsterRank.Elite or MonsterRank.Boss))
        {
            return Failure(CombatErrorCodes.UnsupportedMonster, character.Id);
        }

        LocationDefinition? currentLocation = indexes.LocationsById.GetValueOrDefault(expectedLocationId);
        if (currentLocation is null
            || currentLocation.Encounters?.Any(encounter =>
                string.Equals(encounter.MonsterId, monster.Id, StringComparison.Ordinal)) != true)
        {
            return Failure(CombatErrorCodes.InvalidLocation, character.Id);
        }

        DateTimeOffset startedAtUtc = timeProvider.GetUtcNow();
        CharacterDerivedState leaderDerived = await derivedStateService.ResolveAsync(
            character.Id,
            character.ClassId,
            character.Level,
            contentSnapshot,
            cancellationToken);
        (CombatParticipantDefinition player, CombatParticipantDefinition? companion) =
            BuildParticipant(character, leaderDerived);

        MonsterAiProfile ai = indexes.MonsterAiProfilesById.TryGetValue(
            monster.AiProfileId,
            out MonsterAiProfile? resolvedAi)
            ? resolvedAi
            : throw new InvalidOperationException(
                $"Monster AI profile '{monster.AiProfileId}' is missing from game content.");
        CombatParticipantDefinition enemy = new(
            new CombatActorState(
                Guid.NewGuid(),
                monster.MaxHp,
                monster.MaxHp,
                0,
                0,
                monster.Stats),
            CombatActorKind.Monster,
            monster.Id,
            monster.DisplayName ?? monster.Name,
            "NONE",
            new AutoAttackProfile(
                monster.AutoAttackInterval,
                monster.AutoAttackBaseDamage,
                monster.AutoAttackAttackPowerCoefficient,
                0,
                monster.AutoAttackBaseDamageMin,
                monster.AutoAttackBaseDamageMax),
            new HashSet<string>(monster.AbilityIds, StringComparer.Ordinal),
            MonsterRank: monster.Rank);

        Dictionary<string, AbilityDefinition> abilities = (content.Abilities ?? [])
            .ToDictionary(ability => ability.Id, StringComparer.Ordinal);
        CombatSummonProfile? summonProfile = ResolveSummonProfile(monster, indexes);
        IReadOnlyDictionary<string, DateTimeOffset> initialCooldowns =
            await cooldownStore.LoadActiveAsync(
                character.Id,
                startedAtUtc,
                cancellationToken);

        List<CombatPlayerDefinition> additionalPlayers = [];
        foreach (RaidCombatMember member in capturedRoster.Where(member => !member.IsLeader))
        {
            BootstrapSnapshot memberBootstrap = await bootstrapService.GetAsync(
                member.AccountId,
                contentSnapshot,
                cancellationToken,
                checkpoint: true);
            if (memberBootstrap.Character is null
                || memberBootstrap.Character.Id != member.CharacterId
                || memberBootstrap.Character.Vitals.CurrentHp <= 0
                || memberBootstrap.AfkFarm?.Status == Elyndor.Core.Afk.AfkFarmStatus.Active
                || memberBootstrap.World is null
                || memberBootstrap.World.Travel is not null
                || !string.Equals(
                    memberBootstrap.World.CurrentLocation.Id,
                    expectedLocationId,
                    StringComparison.Ordinal)
                || !PlayableCombatClassIds.Contains(memberBootstrap.Character.ClassId))
            {
                return Failure(CombatErrorCodes.InvalidLocation, character.Id);
            }

            CharacterDerivedState derived = await derivedStateService.ResolveAsync(
                member.CharacterId,
                memberBootstrap.Character.ClassId,
                memberBootstrap.Character.Level,
                contentSnapshot,
                cancellationToken);
            (CombatParticipantDefinition definition, _) = BuildParticipant(
                memberBootstrap.Character,
                derived);
            IReadOnlyDictionary<string, DateTimeOffset> cooldowns =
                await cooldownStore.LoadActiveAsync(
                    member.CharacterId,
                    startedAtUtc,
                    cancellationToken);
            additionalPlayers.Add(new CombatPlayerDefinition(
                member.AccountId,
                definition,
                derived.TalentModifiers,
                cooldowns,
                InitiallyAttached: true));
        }

        CombatSession session = new(
            Guid.NewGuid(),
            player,
            enemy,
            abilities,
            ai,
            leaderDerived.TalentModifiers,
            randomFactory.Create(),
            startedAtUtc,
            contentSnapshot.ContentVersion,
            contentSnapshot.BalanceVersion,
            initialCooldowns,
            summonProfile,
            companion,
            leaderAccountId,
            additionalPlayers,
            CombatGroupContext.Raid,
            CombatParticipantLimit.MaximumRaid);
        GenericEncounterCombatConfigurator.Configure(session, monster.Id, contentSnapshot);

        CombatSessionParticipant[] participants = capturedRoster
            .Select(member => new CombatSessionParticipant(member.AccountId, member.CharacterId))
            .ToArray();
        return new CombatSessionCreationResult(
            true,
            null,
            character.Id,
            session,
            contentSnapshot,
            participants);
    }

    private static (CombatParticipantDefinition Player, CombatParticipantDefinition? Companion)
        BuildParticipant(
            BootstrapCharacter character,
            CharacterDerivedState derived)
    {
        ClassProfile classProfile = derived.ClassProfile;
        if (classProfile.CombatAutoAttack is null)
            throw new InvalidOperationException(
                $"Class {classProfile.Id} has no combat auto attack profile.");

        InventoryItemSnapshot? mainHandItem = GetEquippedItem(
            derived.Inventory,
            EquipmentSlot.MainHand);
        InventoryItemSnapshot? offHandItem = GetEquippedItem(
            derived.Inventory,
            EquipmentSlot.OffHand);
        decimal attackSpeedMultiplier = Math.Max(0.1m, derived.Stats.AttackSpeed);
        AutoAttackProfile mainHand = BuildPlayerAutoAttackProfile(
            classProfile.CombatAutoAttack,
            mainHandItem,
            attackSpeedMultiplier,
            CombatWeaponHand.MainHand);
        bool canDualWield = derived.TalentTree is not null
            && TalentEquipmentPermissionResolver.HasPermission(
                derived.TalentTree,
                derived.ActiveTalentRanks,
                EquipmentPermissionIds.DualWieldOneHandWeapon);
        AutoAttackProfile? offHand = canDualWield
            && offHandItem is not null
            && EquipmentCategoryIds.IsOneHandedWeapon(offHandItem.Definition.WeaponCategory)
                ? BuildPlayerAutoAttackProfile(
                    classProfile.CombatAutoAttack,
                    offHandItem,
                    attackSpeedMultiplier,
                    CombatWeaponHand.OffHand)
                : null;

        CombatParticipantDefinition player = new(
            new CombatActorState(
                character.Id,
                character.Vitals.MaxHp,
                character.Vitals.CurrentHp,
                character.Vitals.MaxResource,
                character.Vitals.CurrentResource,
                ToCombatStats(character.Level, character.Stats),
                derived.TalentModifiers.Combat),
            CombatActorKind.Player,
            character.ClassId,
            character.Name,
            character.Vitals.ResourceType,
            mainHand,
            new HashSet<string>(derived.KnownAbilityIds, StringComparer.Ordinal),
            derived.EffectiveResourceProfile.CombatRegenPerSecond,
            CanAutoAttack: classProfile.AllowUnarmed
                || mainHandItem?.Definition.WeaponCategory is not null,
            OffHandAutoAttack: offHand);
        CombatParticipantDefinition? companion = derived.ActiveCompanionProfile is null
            ? null
            : ArcherCompanionRuntimeResolver.Resolve(
                derived.ActiveCompanionProfile,
                derived.Stats,
                character.Level,
                derived.TalentModifiers);
        return (player, companion);
    }

    private static CombatSummonProfile? ResolveSummonProfile(
        MonsterDefinition monster,
        GameContentIndexes indexes)
    {
        if (string.IsNullOrWhiteSpace(monster.SummonMonsterId))
            return null;
        if (monster.SummonIntervalSeconds <= 0
            || monster.SummonCount <= 0
            || monster.MaxActiveSummons <= 0
            || !indexes.MonstersById.TryGetValue(monster.SummonMonsterId, out MonsterDefinition? summonedMonster)
            || !indexes.MonsterAiProfilesById.TryGetValue(summonedMonster.AiProfileId, out MonsterAiProfile? summonedAi))
        {
            throw new InvalidOperationException(
                $"Monster summon profile for '{monster.Id}' is invalid.");
        }

        return new CombatSummonProfile(
            monster.Id,
            summonedMonster,
            summonedAi,
            TimeSpan.FromSeconds((double)monster.SummonIntervalSeconds),
            monster.SummonCount,
            monster.MaxActiveSummons);
    }

    private static InventoryItemSnapshot? GetEquippedItem(
        InventorySnapshot inventory,
        EquipmentSlot slot)
    {
        if (inventory.Equipped.TryGetValue(slot, out InventoryItemSnapshot? item))
            return item;
        if (slot == EquipmentSlot.MainHand
            && inventory.Equipped.TryGetValue(EquipmentSlot.Weapon, out item))
        {
            return item;
        }
        return null;
    }

    private static AutoAttackProfile BuildPlayerAutoAttackProfile(
        AutoAttackProfile classProfile,
        InventoryItemSnapshot? weapon,
        decimal attackSpeedMultiplier,
        CombatWeaponHand hand)
    {
        decimal baseIntervalSeconds = weapon?.Definition.WeaponBaseAttackIntervalSeconds
            ?? (decimal)classProfile.Interval.TotalSeconds;
        return classProfile with
        {
            Interval = TimeSpan.FromSeconds(
                (double)(baseIntervalSeconds / Math.Max(0.1m, attackSpeedMultiplier))),
            BaseDamageMin = weapon?.Definition.WeaponDamageMin
                ?? classProfile.BaseDamageMin,
            BaseDamageMax = weapon?.Definition.WeaponDamageMax
                ?? classProfile.BaseDamageMax,
            WeaponDefinitionId = weapon?.Definition.Id,
            WeaponHand = hand
        };
    }

    private static CombatStats ToCombatStats(
        int level,
        Elyndor.Core.Characters.CharacterStats stats) => new(
        level,
        stats.Accuracy,
        stats.Dodge,
        stats.CriticalChance,
        stats.CriticalDamage / 100m,
        stats.Armor,
        stats.MagicResistance,
        stats.ArmorPenetration / 100m,
        stats.MagicPenetration / 100m,
        stats.AttackPower,
        stats.SpellPower,
        stats.BlockChance,
        stats.BlockValueMin,
        stats.BlockValueMax);

    private static CombatSessionCreationResult Failure(
        string code,
        Guid characterId = default) =>
        new(false, code, characterId, null);
}
