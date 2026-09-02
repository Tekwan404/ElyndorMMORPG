using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;
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
    private const string WhisperingForestId = "WHISPERING_FOREST";
    private static readonly HashSet<string> WhisperingForestMonsterIds = new(StringComparer.Ordinal)
    {
        "WOLF",
        "FOREST_BOAR",
        "GIANT_SPIDER"
    };

    public async Task<CombatSessionCreationResult> CreateAsync(
        Guid accountId,
        string monsterId,
        CancellationToken cancellationToken)
    {
        BootstrapSnapshot bootstrap = await bootstrapService.GetAsync(
            accountId,
            cancellationToken,
            checkpoint: true);
        BootstrapCharacter? character = bootstrap.Character;
        if (character is null) return Failure("character_not_found");
        if (!string.Equals(character.ClassId, "WARRIOR", StringComparison.Ordinal))
            return Failure(CombatErrorCodes.UnsupportedClass, character.Id);

        MonsterDefinition? monster = content.Monsters?.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, monsterId, StringComparison.Ordinal));
        if (monster is null || !WhisperingForestMonsterIds.Contains(monster.Id))
            return Failure(CombatErrorCodes.UnsupportedMonster, character.Id);

        if (bootstrap.World is null
            || !string.Equals(
                bootstrap.World.CurrentLocation.Id,
                WhisperingForestId,
                StringComparison.Ordinal))
            return Failure(CombatErrorCodes.InvalidLocation, character.Id);
        if (character.Vitals.CurrentHp <= 0)
            return Failure(CombatErrorCodes.InvalidLocation, character.Id);

        ClassProfile classProfile = content.ClassProfiles!.Single(candidate =>
            string.Equals(candidate.Id, character.ClassId, StringComparison.Ordinal));
        if (classProfile.CombatAutoAttack is null)
            throw new InvalidOperationException(
                $"Class {classProfile.Id} has no combat auto attack profile.");

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

        MonsterAiProfile ai = content.MonsterAiProfiles!.Single(candidate =>
            string.Equals(candidate.Id, monster.AiProfileId, StringComparison.Ordinal));

        TalentOperationResult talents = await talentService.GetAsync(accountId, cancellationToken);
        ResolvedTalentModifiers talentModifiers = talents.IsSuccess
            ? TalentModifierResolver.Resolve(
                talents.Snapshot!.Tree,
                talents.Snapshot.State.GetRanks(talents.Snapshot.State.ActiveLoadoutId))
            : ResolvedTalentModifiers.Empty;

        CombatActorState playerActor = new(
            character.Id,
            character.Vitals.MaxHp,
            character.Vitals.CurrentHp,
            character.Vitals.MaxResource,
            character.Vitals.CurrentResource,
            ToCombatStats(character.Level, character.Stats),
            talentModifiers.Combat);
        CombatActorState enemyActor = new(
            Guid.NewGuid(),
            monster.MaxHp,
            monster.MaxHp,
            0,
            0,
            monster.Stats);
        CombatParticipantDefinition player = new(
            playerActor,
            CombatActorKind.Player,
            character.ClassId,
            character.Name,
            character.Vitals.ResourceType,
            playerAutoAttack,
            new HashSet<string>(character.KnownAbilityIds, StringComparer.Ordinal));
        CombatParticipantDefinition enemy = new(
            enemyActor,
            CombatActorKind.Monster,
            monster.Id,
            monster.Name,
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
