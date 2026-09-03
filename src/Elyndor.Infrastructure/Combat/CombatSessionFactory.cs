using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Talents;
using Elyndor.Infrastructure.World;

namespace Elyndor.Infrastructure.Combat;

public sealed record CombatSessionCreationResult(
    bool Succeeded,
    string? ErrorCode,
    Guid CharacterId,
    CombatSession? Session);

public sealed class CombatSessionFactory(
    BootstrapService bootstrapService,
    TalentService talentService,
    GameContentPackage content,
    IGameRandomFactory randomFactory,
    TimeProvider timeProvider)
{
    public const string TrainingDummyId = "TRAINING_DUMMY";
    public const string StarterTownId = "STARTER_TOWN";
    private const decimal TrainingDummyMaxHp = 10_000m;
    private static readonly HashSet<string> PlayableCombatClassIds = new(StringComparer.Ordinal)
    {
        "WARRIOR",
        "MAGE"
    };

    public async Task<CombatSessionCreationResult> CreateAsync(
        Guid accountId,
        string monsterId,
        string expectedLocationId,
        CancellationToken cancellationToken)
    {
        BootstrapSnapshot bootstrap = await bootstrapService.GetAsync(
            accountId,
            cancellationToken,
            checkpoint: true);
        BootstrapCharacter? character = bootstrap.Character;
        if (character is null) return Failure("character_not_found");
        if (!PlayableCombatClassIds.Contains(character.ClassId))
            return Failure(CombatErrorCodes.UnsupportedClass, character.Id);

        bool isTraining = string.Equals(monsterId, TrainingDummyId, StringComparison.Ordinal);
        MonsterDefinition? monster = isTraining
            ? CreateTrainingDummy(character.Level)
            : content.Monsters?.SingleOrDefault(candidate =>
                string.Equals(candidate.Id, monsterId, StringComparison.Ordinal));
        if (monster is null || !isTraining && monster.Rank != MonsterRank.Normal)
            return Failure(CombatErrorCodes.UnsupportedMonster, character.Id);

        if (bootstrap.World is null
            || !string.Equals(
                bootstrap.World.CurrentLocation.Id,
                expectedLocationId,
                StringComparison.Ordinal))
            return Failure(CombatErrorCodes.InvalidLocation, character.Id);

        LocationDefinition? currentLocation = content.Locations.SingleOrDefault(location =>
            string.Equals(location.Id, expectedLocationId, StringComparison.Ordinal));
        if (currentLocation is null)
            return Failure(CombatErrorCodes.InvalidLocation, character.Id);

        if (isTraining)
        {
            if (!string.Equals(expectedLocationId, StarterTownId, StringComparison.Ordinal))
                return Failure(CombatErrorCodes.InvalidLocation, character.Id);
        }
        else if (currentLocation.Encounters?.Any(encounter =>
                     string.Equals(encounter.MonsterId, monster.Id, StringComparison.Ordinal)) != true)
        {
            return Failure(CombatErrorCodes.InvalidLocation, character.Id);
        }

        if (character.Vitals.CurrentHp <= 0)
            return Failure(CombatErrorCodes.InvalidLocation, character.Id);

        ClassProfile classProfile = content.ClassProfiles!.Single(candidate =>
            string.Equals(candidate.Id, character.ClassId, StringComparison.Ordinal));
        if (classProfile.CombatAutoAttack is null)
            throw new InvalidOperationException(
                $"Class {classProfile.Id} has no combat auto attack profile.");
        ResourceProfile resourceProfile = content.ResourceProfiles!.Single(candidate =>
            string.Equals(candidate.Id, classProfile.ResourceProfileId, StringComparison.Ordinal));

        EquipmentModifierSummary equipment = EquipmentStatModifierResolver.ResolveDetailed(
            character.Inventory.Equipped.Values.Select(item => item.Definition),
            content.EquipmentSets ?? []);
        decimal weaponBaseIntervalSeconds = equipment.WeaponBaseAttackIntervalSeconds
            ?? (decimal)classProfile.CombatAutoAttack.Interval.TotalSeconds;
        decimal attackSpeedMultiplier = Math.Max(0.1m, character.Stats.AttackSpeed);
        AutoAttackProfile playerAutoAttack = classProfile.CombatAutoAttack with
        {
            Interval = TimeSpan.FromSeconds(
                (double)(weaponBaseIntervalSeconds / attackSpeedMultiplier))
        };

        MonsterAiProfile ai = isTraining
            ? new MonsterAiProfile("TRAINING_DUMMY_AI", [])
            : content.MonsterAiProfiles!.Single(candidate =>
                string.Equals(candidate.Id, monster.AiProfileId, StringComparison.Ordinal));

        TalentOperationResult talents = await talentService.GetAsync(accountId, cancellationToken);
        ResolvedTalentModifiers talentModifiers = talents.IsSuccess
            ? TalentModifierResolver.Resolve(
                talents.Snapshot!.Tree,
                talents.Snapshot.State.GetRanks(talents.Snapshot.State.ActiveLoadoutId))
            : ResolvedTalentModifiers.Empty;

        decimal playerHp = isTraining
            ? character.Vitals.MaxHp
            : character.Vitals.CurrentHp;
        decimal playerResource = isTraining
            ? resourceProfile.StartValue
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
            new HashSet<string>(character.KnownAbilityIds, StringComparer.Ordinal),
            resourceProfile.CombatRegenPerSecond);
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
                0),
            new HashSet<string>(monster.AbilityIds, StringComparer.Ordinal));
        Dictionary<string, AbilityDefinition> abilities = (content.Abilities ?? [])
            .ToDictionary(ability => ability.Id, StringComparer.Ordinal);

        CombatSession session = new(
            Guid.NewGuid(),
            player,
            enemy,
            abilities,
            ai,
            talentModifiers,
            randomFactory.Create(),
            timeProvider.GetUtcNow());
        return new CombatSessionCreationResult(true, null, character.Id, session);
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
        stats.SpellPower);

    private static CombatSessionCreationResult Failure(
        string code,
        Guid characterId = default) => new(false, code, characterId, null);
}
