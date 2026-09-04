using Elyndor.Core.Content;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.World;
using Elyndor.Core.Talents;
using Elyndor.Core.Monsters;
using Elyndor.Core.Items;
using Elyndor.Core.Progression;

namespace Elyndor.UnitTests.Content;

public sealed class GameContentPackageValidatorTests
{
    private static readonly DateTimeOffset PublishedAtUtc =
        new(2026, 8, 29, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ValidateAcceptsTypedReferencesToExistingDefinitions()
    {
        GameContentPackage package = CreatePackage(
            new GameContentDefinition(
                "CLASS",
                "WARRIOR",
                [new GameContentReference("ABILITY", "BASIC_ATTACK")]),
            new GameContentDefinition("ABILITY", "BASIC_ATTACK", []));

        IReadOnlyList<ContentValidationError> errors =
            GameContentPackageValidator.Validate(package);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateRejectsDuplicateIdsWithinTheSameDefinitionType()
    {
        GameContentPackage package = CreatePackage(
            new GameContentDefinition("CLASS", "WARRIOR", []),
            new GameContentDefinition("CLASS", "WARRIOR", []));

        IReadOnlyList<ContentValidationError> errors =
            GameContentPackageValidator.Validate(package);

        Assert.Contains(errors, error => error.Code == "DUPLICATE_DEFINITION_ID");
    }

    [Fact]
    public void ValidateRejectsMissingTypedReference()
    {
        GameContentPackage package = CreatePackage(
            new GameContentDefinition(
                "CLASS",
                "WARRIOR",
                [new GameContentReference("ABILITY", "MISSING_ABILITY")]));

        IReadOnlyList<ContentValidationError> errors =
            GameContentPackageValidator.Validate(package);

        Assert.Contains(errors, error => error.Code == "MISSING_REFERENCE");
    }

    [Theory]
    [InlineData("class", "WARRIOR", "INVALID_DEFINITION_TYPE")]
    [InlineData("CLASS", "warrior", "INVALID_DEFINITION_ID")]
    public void ValidateRejectsNonCanonicalIdentifiers(
        string definitionType,
        string definitionId,
        string expectedErrorCode)
    {
        GameContentPackage package = CreatePackage(
            new GameContentDefinition(definitionType, definitionId, []));

        IReadOnlyList<ContentValidationError> errors =
            GameContentPackageValidator.Validate(package);

        Assert.Contains(errors, error => error.Code == expectedErrorCode);
    }

    [Fact]
    public void ValidateRejectsMissingVersionsAndNonUtcPublicationTime()
    {
        GameContentPackage package = new(
            string.Empty,
            " ",
            PublishedAtUtc.ToOffset(TimeSpan.FromHours(5)),
            [],
            []);

        IReadOnlyList<ContentValidationError> errors =
            GameContentPackageValidator.Validate(package);

        Assert.Contains(errors, error => error.Code == "MISSING_CONTENT_VERSION");
        Assert.Contains(errors, error => error.Code == "MISSING_BALANCE_VERSION");
        Assert.Contains(errors, error => error.Code == "PUBLISHED_AT_NOT_UTC");
    }

    [Fact]
    public void ValidateRejectsDuplicateLocationIds()
    {
        GameContentPackage package = CreatePackageWithLocations(
            CreateLocation("STARTER_TOWN"),
            CreateLocation("STARTER_TOWN"));

        IReadOnlyList<ContentValidationError> errors =
            GameContentPackageValidator.Validate(package);

        Assert.Contains(errors, error => error.Code == "DUPLICATE_LOCATION_ID");
    }

    [Fact]
    public void ValidateRejectsMissingAndSelfTransitions()
    {
        GameContentPackage package = CreatePackageWithLocations(
            CreateLocation("STARTER_TOWN", transitions: ["STARTER_TOWN", "MISSING"]));

        IReadOnlyList<ContentValidationError> errors =
            GameContentPackageValidator.Validate(package);

        Assert.Contains(errors, error => error.Code == "SELF_LOCATION_TRANSITION");
        Assert.Contains(errors, error => error.Code == "MISSING_LOCATION_TRANSITION");
    }

    [Theory]
    [InlineData("UNKNOWN", 1, "INVALID_LOCATION_DANGER_LEVEL")]
    [InlineData("SAFE", 0, "INVALID_LOCATION_RECOMMENDED_LEVEL")]
    public void ValidateRejectsInvalidLocationGameplayMetadata(
        string dangerLevel,
        int recommendedLevel,
        string expectedErrorCode)
    {
        GameContentPackage package = CreatePackageWithLocations(
            CreateLocation(
                "STARTER_TOWN",
                dangerLevel,
                recommendedLevel));

        IReadOnlyList<ContentValidationError> errors =
            GameContentPackageValidator.Validate(package);

        Assert.Contains(errors, error => error.Code == expectedErrorCode);
    }

    [Fact]
    public void ValidateAcceptsOptionalCombatDefinitions()
    {
        GameContentPackage package = CreatePackage() with
        {
            Effects =
            [
                new EffectDefinition("TEST_BURN", EffectKind.DamageOverTime,
                    TimeSpan.FromSeconds(4), 1, EffectStackPolicy.Refresh, 5,
                    TimeSpan.FromSeconds(1))
            ],
            Abilities =
            [
                new AbilityDefinition("TEST_FIREBALL", AbilityType.Casted,
                    AbilityTargetType.SingleEnemy, 10, TimeSpan.FromSeconds(4),
                    TimeSpan.FromSeconds(2), true, GlobalCooldownCategory.Standard,
                    true, "FIRE")
            ]
        };

        Assert.Empty(GameContentPackageValidator.Validate(package));
    }

    [Fact]
    public void ValidateRejectsDuplicateAndInvalidCombatDefinitions()
    {
        EffectDefinition effect = new("TEST_EFFECT", EffectKind.Buff,
            TimeSpan.Zero, 0, EffectStackPolicy.Stack, -1);
        AbilityDefinition ability = new("TEST_ABILITY", AbilityType.Instant,
            AbilityTargetType.Self, -1, TimeSpan.Zero, TimeSpan.Zero, true,
            GlobalCooldownCategory.None, false, "PHYSICAL");
        GameContentPackage package = CreatePackage() with
        {
            Effects = [effect, effect],
            Abilities = [ability, ability]
        };

        IReadOnlyList<ContentValidationError> errors = GameContentPackageValidator.Validate(package);

        Assert.Contains(errors, error => error.Code == "DUPLICATE_EFFECT_ID");
        Assert.Contains(errors, error => error.Code == "INVALID_EFFECT_DEFINITION");
        Assert.Contains(errors, error => error.Code == "DUPLICATE_ABILITY_ID");
        Assert.Contains(errors, error => error.Code == "INVALID_ABILITY_DEFINITION");
    }

    [Fact]
    public void ValidateRejectsCircularTalentPrerequisites()
    {
        GameContentPackage package = CreatePackage() with
        {
            TalentTrees =
            [
                new TalentTreeDefinition(
                    "TEST_TREE", "WARRIOR", 59, 1,
                    [new TalentBranchDefinition("GUARDIAN", "Страж", "", 2)],
                    [
                        new TalentDefinition("A", "GUARDIAN", 1, 0, "A", "A", 1,
                            [new TalentPrerequisite("B")], ""),
                        new TalentDefinition("B", "GUARDIAN", 1, 0, "B", "B", 1,
                            [new TalentPrerequisite("A")], "")
                    ])
            ]
        };

        IReadOnlyList<ContentValidationError> errors = GameContentPackageValidator.Validate(package);

        Assert.Contains(errors, error => error.Code == "CIRCULAR_TALENT_PREREQUISITE");
    }

    [Fact]
    public void ValidateRejectsSupportedTalentAbilityReferenceThatDoesNotExist()
    {
        GameContentPackage package = CreatePackage() with
        {
            TalentTrees =
            [
                new TalentTreeDefinition(
                    "TEST_TREE", "WARRIOR", 59, 1,
                    [new TalentBranchDefinition("BERSERKER", "Берсерк", "", 1)],
                    [
                        new TalentDefinition(
                            "B-1-1", "BERSERKER", 1, 0, "Удар", "Strike", 1, [], "",
                            Modifiers:
                            [
                                new TalentModifierDefinition(
                                    TalentModifierType.AbilityModifier,
                                    TalentModifierKeys.UnlockAbility,
                                    [1],
                                    "MISSING_ABILITY")
                            ],
                            IconId: "BERSERKER_STRIKE")
                    ])
            ]
        };

        IReadOnlyList<ContentValidationError> errors = GameContentPackageValidator.Validate(package);

        Assert.Contains(errors, error => error.Code == "MISSING_TALENT_ABILITY_REFERENCE");
    }

    [Fact]
    public void ValidateRejectsDescriptionOnlyTalentNode()
    {
        GameContentPackage package = CreatePackage() with
        {
            TalentTrees =
            [
                new TalentTreeDefinition(
                    "TEST_TREE", "WARRIOR", 59, 1,
                    [new TalentBranchDefinition("BERSERKER", "Берсерк", "", 1)],
                    [new TalentDefinition("B-1-1", "BERSERKER", 1, 0, "Удар", "Strike", 1, [], "")])
            ]
        };

        IReadOnlyList<ContentValidationError> errors = GameContentPackageValidator.Validate(package);

        Assert.Contains(errors, error => error.Code == "MISSING_TALENT_MODIFIER");
    }

    [Fact]
    public void ValidateRejectsMonsterWithMissingAbilityAndAiProfile()
    {
        GameContentPackage package = CreatePackage() with
        {
            Monsters =
            [
                new MonsterDefinition(
                    "WOLF", "Wolf", MonsterRank.Normal, 3, 180,
                    CombatStats.Default, TimeSpan.FromSeconds(2.5), 8,
                    ["MISSING_BITE"], "MISSING_AI")
            ],
            MonsterAiProfiles = []
        };

        IReadOnlyList<ContentValidationError> errors =
            GameContentPackageValidator.Validate(package);

        Assert.Contains(errors, error => error.Code == "MISSING_MONSTER_ABILITY");
        Assert.Contains(errors, error => error.Code == "MISSING_MONSTER_AI_PROFILE");
    }

    [Fact]
    public void ValidateRejectsPhaseFiveLootWithMissingItemReference()
    {
        GameContentPackage package = CreatePackage() with
        {
            LevelProgression = new LevelProgressionDefinition("DEFAULT_LEVELING", 60, 100, 1.5m),
            Items =
            [
                new ItemDefinition(
                    "WOLF_HIDE", "Wolf Hide", ItemType.Material, ItemRarity.Common,
                    1, true, 99, null, new PrimaryStats(0, 0, 0, 0), "Material")
            ],
            LootTables =
            [
                new LootTableDefinition(
                    "WOLF_LOOT",
                    [new LootTableEntry("MISSING_ITEM", 1m, 1, 1)])
            ]
        };

        IReadOnlyList<ContentValidationError> errors =
            GameContentPackageValidator.Validate(package);

        Assert.Contains(errors, error => error.Code == "MISSING_LOOT_ITEM_REFERENCE");
    }

    [Fact]
    public void ValidateAcceptsHealingConsumableDefinition()
    {
        GameContentPackage package = CreatePackage() with
        {
            LevelProgression = new LevelProgressionDefinition("DEFAULT_LEVELING", 60, 100, 1.5m),
            Items =
            [
                new ItemDefinition(
                    "SMALL_HEALING_POTION",
                    "Small Healing Potion",
                    ItemType.Consumable,
                    ItemRarity.Common,
                    1,
                    true,
                    20,
                    null,
                    new PrimaryStats(0, 0, 0, 0),
                    "Restores health.",
                    HealAmount: 50,
                    ConsumableCooldownSeconds: 30,
                    BuyPriceGold: 20)
            ],
            LootTables = []
        };

        IReadOnlyList<ContentValidationError> errors =
            GameContentPackageValidator.Validate(package);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateRejectsUnknownEquipmentCategory()
    {
        GameContentPackage package = CreatePackage() with
        {
            LevelProgression = new LevelProgressionDefinition("DEFAULT_LEVELING", 60, 100, 1.5m),
            Items =
            [
                new ItemDefinition(
                    "TEST_SWORD", "Test Sword", ItemType.Equipment, ItemRarity.Common,
                    1, false, 1, EquipmentSlot.Weapon,
                    new PrimaryStats(1, 0, 0, 0), "Test",
                    WeaponCategory: "LASER_SWORD")
            ],
            LootTables = []
        };

        IReadOnlyList<ContentValidationError> errors =
            GameContentPackageValidator.Validate(package);

        Assert.Contains(errors, error => error.Code == "INVALID_ITEM_EQUIPMENT_CATEGORY");
    }

    [Fact]
    public void ValidateRejectsUnknownItemClassRestriction()
    {
        GameContentPackage package = CreatePackage() with
        {
            LevelProgression = new LevelProgressionDefinition("DEFAULT_LEVELING", 60, 100, 1.5m),
            Items =
            [
                new ItemDefinition(
                    "TEST_SWORD", "Test Sword", ItemType.Equipment, ItemRarity.Common,
                    1, false, 1, EquipmentSlot.Weapon,
                    new PrimaryStats(1, 0, 0, 0), "Test",
                    WeaponCategory: EquipmentCategoryIds.OneHandSword,
                    AllowedClassIds: ["MISSING_CLASS"])
            ],
            LootTables = []
        };

        IReadOnlyList<ContentValidationError> errors =
            GameContentPackageValidator.Validate(package);

        Assert.Contains(errors, error => error.Code == "INVALID_ITEM_CLASS_RESTRICTION");
    }

    [Fact]
    public void ValidateRejectsInvalidTypedTalentRuntimeParameters()
    {
        GameContentPackage package = CreatePackage() with
        {
            TalentTrees =
            [
                new TalentTreeDefinition(
                    "TEST_TREE",
                    "WARRIOR",
                    59,
                    1,
                    [new TalentBranchDefinition("BERSERKER", "Берсерк", "", 1)],
                    [
                        new TalentDefinition(
                            "B-4-1",
                            "BERSERKER",
                            1,
                            0,
                            "Double Strike",
                            "Double Strike",
                            1,
                            [],
                            "",
                            Modifiers:
                            [
                                new TalentModifierDefinition(
                                    TalentModifierType.EventTriggered,
                                    TalentModifierKeys.OnAutoAttack,
                                    [45],
                                    RuntimeStatus: TalentModifierRuntimeStatus.Deferred,
                                    DeferredOwner: TalentRuntimeOwners.CombatSession,
                                    SecondaryValues: [1, 2],
                                    ChancePercent: 120,
                                    DurationSeconds: 1,
                                    TickIntervalSeconds: 2)
                            ])
                    ])
            ]
        };

        IReadOnlyList<ContentValidationError> errors =
            GameContentPackageValidator.Validate(package);

        Assert.Contains(errors, error => error.Code == "INVALID_TALENT_MODIFIER");
    }

    private static GameContentPackage CreatePackage(params GameContentDefinition[] definitions) =>
        new("0.1.0", "0.1.0", PublishedAtUtc, definitions, []);

    private static GameContentPackage CreatePackageWithLocations(
        params LocationDefinition[] locations) =>
        new("0.1.0", "0.1.0", PublishedAtUtc, [], locations);

    private static LocationDefinition CreateLocation(
        string id,
        string dangerLevel = "SAFE",
        int recommendedLevel = 1,
        IReadOnlyList<string>? transitions = null) =>
        new(id, id, dangerLevel, recommendedLevel, transitions ?? []);
}
