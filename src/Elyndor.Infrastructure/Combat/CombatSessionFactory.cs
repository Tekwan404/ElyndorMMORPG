using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Participants;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.World;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Parties;

namespace Elyndor.Infrastructure.Combat;

public sealed record CombatSessionParticipant(Guid AccountId, Guid CharacterId);

public sealed record CombatSessionCreationResult(
    bool Succeeded,
    string? ErrorCode,
    Guid CharacterId,
    CombatSession? Session,
    GameContentSnapshot? ContentSnapshot = null,
    IReadOnlyList<CombatSessionParticipant>? Participants = null);

public sealed class CombatSessionFactory(
    BootstrapService bootstrapService,
    CharacterDerivedStateService derivedStateService,
    IContentSnapshotProvider contentProvider,
    IGameRandomFactory randomFactory,
    TimeProvider timeProvider,
    CharacterAbilityCooldownStore? cooldownStore = null,
    PartyService? partyService = null)
{
    public CombatSessionFactory(
        BootstrapService bootstrapService,
        CharacterDerivedStateService derivedStateService,
        GameContentPackage content,
        IGameRandomFactory randomFactory,
        TimeProvider timeProvider)
        : this(
            bootstrapService,
            derivedStateService,
            new StaticContentSnapshotProvider(content),
            randomFactory,
            timeProvider)
    {
    }

    public const string TrainingDummyId = "TRAINING_DUMMY";
    public const string StarterTownId = WorldLocationIds.StarterTown;
    private const decimal TrainingDummyMaxHp = 1_000_000_000m;
    private static readonly HashSet<string> PlayableCombatClassIds = new(StringComparer.Ordinal)
    {
        "WARRIOR",
        "ARCHER",
        "MAGE"
    };

    public async Task<CombatSessionCreationResult> CreateAsync(
        Guid accountId,
        string monsterId,
        string expectedLocationId,
        CancellationToken cancellationToken,
        IReadOnlyList<PartyCombatMember>? partyMembersOverride = null)
    {
        GameContentSnapshot contentSnapshot = contentProvider.GetCurrent();
        GameContentPackage content = contentSnapshot.Package;
        GameContentIndexes indexes = contentSnapshot.Indexes;

        BootstrapSnapshot bootstrap = await bootstrapService.GetAsync(
            accountId,
            contentSnapshot,
            cancellationToken,
            checkpoint: true);
        BootstrapCharacter? character = bootstrap.Character;
        if (character is null) return Failure("character_not_found");
        if (!PlayableCombatClassIds.Contains(character.ClassId))
            return Failure(CombatErrorCodes.UnsupportedClass, character.Id);

        bool isTraining = string.Equals(monsterId, TrainingDummyId, StringComparison.Ordinal);
        MonsterDefinition? monster = isTraining
            ? CreateTrainingDummy(character.Level)
            : indexes.MonstersById.GetValueOrDefault(monsterId);
        if (monster is null
            || !isTraining && monster.Rank is not (MonsterRank.Normal or MonsterRank.Boss))
            return Failure(CombatErrorCodes.UnsupportedMonster, character.Id);

        if (bootstrap.World is null
            || bootstrap.World.Travel is not null
            || !string.Equals(
                bootstrap.World.CurrentLocation.Id,
                expectedLocationId,
                StringComparison.Ordinal))
            return Failure(CombatErrorCodes.InvalidLocation, character.Id);

        LocationDefinition? currentLocation =
            indexes.LocationsById.GetValueOrDefault(expectedLocationId);
        if (currentLocation is null)
            return Failure(CombatErrorCodes.InvalidLocation, character.Id);

        if (isTraining)
        {
            if (!string.Equals(expectedLocationId, StarterTownId, StringComparison.Ordinal))
                return Failure(CombatErrorCodes.InvalidLocation, character.Id);
        }
        else if (currentLocation.Encounters?.Any(encounter =>
                     string.Equals(encounter.MonsterId, monster.Id, StringComparison.Ordinal)) != true
            && content.Dungeons?.Any(dungeon =>
                string.Equals(dungeon.EntryLocationId, expectedLocationId, StringComparison.Ordinal)
                && dungeon.Encounters.Any(encounter =>
                    string.Equals(encounter.MonsterId, monster.Id, StringComparison.Ordinal))) != true)
        {
            return Failure(CombatErrorCodes.InvalidLocation, character.Id);
        }

        if (character.Vitals.CurrentHp <= 0)
            return Failure(CombatErrorCodes.InvalidLocation, character.Id);

        PartyCombatMember[] partyMembers = isTraining
            ? [new(accountId, character.Id, true)]
            : partyMembersOverride?.ToArray()
                ?? (partyService is null
                    ? [new(accountId, character.Id, true)]
                    : (await partyService.GetCombatMembersAsync(accountId, cancellationToken)).ToArray());
        if (partyMembers.Length == 0
            || partyMembers.Length > CombatParticipantRoster.DefaultMaximumParticipants
            || partyMembers.SingleOrDefault(member => member.IsLeader)?.CharacterId != character.Id)
        {
            return Failure(CombatErrorCodes.InvalidLocation, character.Id);
        }

        CharacterDerivedState derived = await derivedStateService.ResolveAsync(
            character.Id,
            character.ClassId,
            character.Level,
            contentSnapshot,
            cancellationToken);
        ClassProfile classProfile = derived.ClassProfile;
        if (classProfile.CombatAutoAttack is null)
            throw new InvalidOperationException(
                $"Class {classProfile.Id} has no combat auto attack profile.");
        ResourceProfile resourceProfile = derived.EffectiveResourceProfile;

        decimal attackSpeedMultiplier = Math.Max(0.1m, derived.Stats.AttackSpeed);
        InventoryItemSnapshot? mainHandItem = GetEquippedItem(
            derived.Inventory,
            EquipmentSlot.MainHand);
        InventoryItemSnapshot? offHandItem = GetEquippedItem(
            derived.Inventory,
            EquipmentSlot.OffHand);
        AutoAttackProfile playerAutoAttack = BuildPlayerAutoAttackProfile(
            classProfile.CombatAutoAttack,
            mainHandItem,
            attackSpeedMultiplier,
            CombatWeaponHand.MainHand);
        bool canDualWield = derived.TalentTree is not null
            && TalentEquipmentPermissionResolver.HasPermission(
                derived.TalentTree,
                derived.ActiveTalentRanks,
                EquipmentPermissionIds.DualWieldOneHandWeapon);
        AutoAttackProfile? offHandAutoAttack = canDualWield
            && offHandItem is not null
            && EquipmentCategoryIds.IsOneHandedWeapon(offHandItem.Definition.WeaponCategory)
                ? BuildPlayerAutoAttackProfile(
                    classProfile.CombatAutoAttack,
                    offHandItem,
                    attackSpeedMultiplier,
                    CombatWeaponHand.OffHand)
                : null;

        MonsterAiProfile ai = isTraining
            ? new MonsterAiProfile("TRAINING_DUMMY_AI", [])
            : indexes.MonsterAiProfilesById.TryGetValue(
                monster.AiProfileId,
                out MonsterAiProfile? resolvedAi)
                ? resolvedAi
                : throw new InvalidOperationException(
                    $"Monster AI profile '{monster.AiProfileId}' is missing from game content.");

        ResolvedTalentModifiers talentModifiers = derived.TalentModifiers;

        decimal playerHp = isTraining
            ? character.Vitals.MaxHp
            : character.Vitals.CurrentHp;
        decimal playerResource = isTraining
            ? character.Vitals.MaxResource
            : character.Vitals.CurrentResource;
        CombatActorState playerActor = new(
            character.Id,
            character.Vitals.MaxHp,
            playerHp,
            character.Vitals.MaxResource,
            playerResource,
            ToCombatStats(character.Level, character.Stats),
            talentModifiers.Combat);
        CombatActorState enemyActor = new(
            Guid.NewGuid(),
            monster.MaxHp,
            monster.MaxHp,
            0,
            0,
            monster.Stats,
            canDie: !isTraining);
        CombatParticipantDefinition player = new(
            playerActor,
            CombatActorKind.Player,
            character.ClassId,
            character.Name,
            character.Vitals.ResourceType,
            playerAutoAttack,
            new HashSet<string>(derived.KnownAbilityIds, StringComparer.Ordinal),
            resourceProfile.CombatRegenPerSecond,
            CanAutoAttack: classProfile.AllowUnarmed
                || mainHandItem?.Definition.WeaponCategory is not null,
            OffHandAutoAttack: offHandAutoAttack);
        CombatParticipantDefinition? companion =
            derived.ActiveCompanionProfile is null
                ? null
                : ArcherCompanionRuntimeResolver.Resolve(
                    derived.ActiveCompanionProfile,
                    derived.Stats,
                    character.Level,
                    talentModifiers);

        CombatParticipantDefinition enemy = new(
            enemyActor,
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
            new HashSet<string>(monster.AbilityIds, StringComparer.Ordinal));
        Dictionary<string, AbilityDefinition> abilities = (content.Abilities ?? [])
            .ToDictionary(ability => ability.Id, StringComparer.Ordinal);

        CombatSummonProfile? summonProfile = null;
        if (!isTraining && !string.IsNullOrWhiteSpace(monster.SummonMonsterId))
        {
            if (monster.SummonIntervalSeconds <= 0
                || monster.SummonCount <= 0
                || monster.MaxActiveSummons <= 0
                || !indexes.MonstersById.TryGetValue(
                    monster.SummonMonsterId,
                    out MonsterDefinition? summonedMonster)
                || !indexes.MonsterAiProfilesById.TryGetValue(
                    summonedMonster.AiProfileId,
                    out MonsterAiProfile? summonedAi))
            {
                throw new InvalidOperationException(
                    $"Monster summon profile for '{monster.Id}' is invalid.");
            }

            summonProfile = new CombatSummonProfile(
                monster.Id,
                summonedMonster,
                summonedAi,
                TimeSpan.FromSeconds((double)monster.SummonIntervalSeconds),
                monster.SummonCount,
                monster.MaxActiveSummons);
        }

        DateTimeOffset startedAtUtc = timeProvider.GetUtcNow();
        List<CombatPlayerDefinition> additionalPlayers = [];
        foreach (PartyCombatMember member in partyMembers.Where(item => item.CharacterId != character.Id))
        {
            BootstrapSnapshot memberBootstrap = await bootstrapService.GetAsync(
                member.AccountId,
                contentSnapshot,
                cancellationToken,
                checkpoint: true);
            if (memberBootstrap.Character is null
                || memberBootstrap.World is null
                || memberBootstrap.Character.Vitals.CurrentHp <= 0
                || !PlayableCombatClassIds.Contains(memberBootstrap.Character.ClassId))
            {
                return Failure(CombatErrorCodes.InvalidLocation, character.Id);
            }

            bool initiallyAttached = memberBootstrap.World.Travel is null
                && string.Equals(
                    memberBootstrap.World.CurrentLocation.Id,
                    expectedLocationId,
                    StringComparison.Ordinal);

            additionalPlayers.Add(await CreatePlayerDefinitionAsync(
                memberBootstrap,
                contentSnapshot,
                isTraining: false,
                startedAtUtc,
                cancellationToken,
                initiallyAttached));
        }

        IReadOnlyDictionary<string, DateTimeOffset> initialCooldowns =
            isTraining || cooldownStore is null
                ? new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal)
                : await cooldownStore.LoadActiveAsync(
                    character.Id,
                    startedAtUtc,
                    cancellationToken);

        CombatSession session = new(
            Guid.NewGuid(),
            player,
            enemy,
            abilities,
            ai,
            talentModifiers,
            randomFactory.Create(),
            startedAtUtc,
            contentSnapshot.ContentVersion,
            contentSnapshot.BalanceVersion,
            initialCooldowns,
            summonProfile,
            companion,
            accountId,
            additionalPlayers);
        CombatSessionParticipant[] participants = partyMembers
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

    private async Task<CombatPlayerDefinition> CreatePlayerDefinitionAsync(
        BootstrapSnapshot bootstrap,
        GameContentSnapshot contentSnapshot,
        bool isTraining,
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken,
        bool initiallyAttached = true)
    {
        BootstrapCharacter character = bootstrap.Character
            ?? throw new InvalidOperationException("Combat participant character is missing.");
        CharacterDerivedState derived = await derivedStateService.ResolveAsync(
            character.Id,
            character.ClassId,
            character.Level,
            contentSnapshot,
            cancellationToken);
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
        AutoAttackProfile playerAutoAttack = BuildPlayerAutoAttackProfile(
            classProfile.CombatAutoAttack,
            mainHandItem,
            attackSpeedMultiplier,
            CombatWeaponHand.MainHand);
        AutoAttackProfile? offHandAutoAttack = derived.TalentTree is not null
            && TalentEquipmentPermissionResolver.HasPermission(
                derived.TalentTree,
                derived.ActiveTalentRanks,
                EquipmentPermissionIds.DualWieldOneHandWeapon)
            && offHandItem is not null
            && EquipmentCategoryIds.IsOneHandedWeapon(offHandItem.Definition.WeaponCategory)
                ? BuildPlayerAutoAttackProfile(
                    classProfile.CombatAutoAttack,
                    offHandItem,
                    attackSpeedMultiplier,
                    CombatWeaponHand.OffHand)
                : null;

        decimal playerHp = isTraining ? character.Vitals.MaxHp : character.Vitals.CurrentHp;
        decimal playerResource = isTraining
            ? character.Vitals.MaxResource
            : character.Vitals.CurrentResource;
        CombatActorState actor = new(
            character.Id,
            character.Vitals.MaxHp,
            playerHp,
            character.Vitals.MaxResource,
            playerResource,
            ToCombatStats(character.Level, character.Stats),
            derived.TalentModifiers.Combat);
        CombatParticipantDefinition participant = new(
            actor,
            CombatActorKind.Player,
            character.ClassId,
            character.Name,
            character.Vitals.ResourceType,
            playerAutoAttack,
            new HashSet<string>(derived.KnownAbilityIds, StringComparer.Ordinal),
            derived.EffectiveResourceProfile.CombatRegenPerSecond,
            CanAutoAttack: classProfile.AllowUnarmed
                || mainHandItem?.Definition.WeaponCategory is not null,
            OffHandAutoAttack: offHandAutoAttack);
        IReadOnlyDictionary<string, DateTimeOffset> cooldowns =
            isTraining || cooldownStore is null
                ? new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal)
                : await cooldownStore.LoadActiveAsync(
                    character.Id,
                    startedAtUtc,
                    cancellationToken);
        return new CombatPlayerDefinition(
            bootstrap.AccountId,
            participant,
            derived.TalentModifiers,
            cooldowns,
            initiallyAttached);
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

    private static MonsterDefinition CreateTrainingDummy(int level) => new(
        TrainingDummyId,
        "Тренировочный манекен",
        MonsterRank.Normal,
        level,
        TrainingDummyMaxHp,
        new CombatStats(
            level,
            Accuracy: 0,
            Dodge: 0,
            CriticalChance: 0,
            CriticalDamage: 1,
            Armor: 0,
            MagicResistance: 0,
            ArmorPenetration: 0,
            MagicPenetration: 0),
        TimeSpan.FromDays(1),
        AutoAttackBaseDamage: 0,
        AbilityIds: [],
        AiProfileId: "TRAINING_DUMMY_AI",
        AutoAttackAttackPowerCoefficient: 0,
        XpReward: 0,
        LootTableId: null,
        GoldRewardMin: 0,
        GoldRewardMax: 0);

    private static CombatStats ToCombatStats(int level, Core.Characters.CharacterStats stats) => new(
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
        Guid characterId = default) => new(false, code, characterId, null);
}
