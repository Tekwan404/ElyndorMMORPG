using Elyndor.Core.Pvp;

namespace Elyndor.UnitTests.Pvp;

public sealed class WorldPvpRulesTests
{
    private static readonly Guid HumanId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid UndeadId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid HumanAccount = Guid.Parse("30000000-0000-0000-0000-000000000003");
    private static readonly Guid UndeadAccount = Guid.Parse("40000000-0000-0000-0000-000000000004");

    private static WorldPvpCombatant Human => new(HumanId, HumanAccount, "HUMAN", 60, "DEAD_REACHES");
    private static WorldPvpCombatant Undead => new(UndeadId, UndeadAccount, "UNDEAD", 60, "DEAD_REACHES");

    [Theory]
    [InlineData("DEAD_REACHES", true)]
    [InlineData("DOOMED_LANDS", false)]
    [InlineData("BLACK_CRAGS", false)]
    [InlineData("STARTER_TOWN", false)]
    [InlineData("UNDEAD_STARTER_TOWN", false)]
    [InlineData("dead_reaches", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void OnlyAuthoredEndgameLocationIsContested(string? locationId, bool expected)
    {
        Assert.Equal(expected, WorldPvpRules.IsContestedLocation(locationId));
    }

    [Fact]
    public void OppositeFactionsAtLevelSixtyCanFightInContestedLocation()
    {
        Assert.Equal(WorldPvpAttackFailure.None, WorldPvpRules.CheckAttack(Human, Undead));
        Assert.Equal(WorldPvpAttackFailure.None, WorldPvpRules.CheckAttack(Undead, Human));
    }

    [Theory]
    [InlineData(59, 60)]
    [InlineData(60, 59)]
    [InlineData(1, 60)]
    public void BothParticipantsMustBeLevelSixty(int attackerLevel, int targetLevel)
    {
        Assert.Equal(WorldPvpAttackFailure.LevelRequired,
            WorldPvpRules.CheckAttack(Human with { Level = attackerLevel },
                Undead with { Level = targetLevel }));
    }

    [Fact]
    public void CannotAttackInSharedPveProgressionZones()
    {
        var attacker = Human with { LocationId = "BLACK_CRAGS" };
        var target = Undead with { LocationId = "BLACK_CRAGS" };

        Assert.Equal(WorldPvpAttackFailure.ZoneUnavailable,
            WorldPvpRules.CheckAttack(attacker, target));
    }

    [Fact]
    public void BothPlayersMustBeInExactlySameLocation()
    {
        Assert.Equal(WorldPvpAttackFailure.DifferentLocation,
            WorldPvpRules.CheckAttack(Human, Undead with { LocationId = "DOOMED_LANDS" }));
    }

    [Fact]
    public void CannotAttackFriendlyFaction()
    {
        Assert.Equal(WorldPvpAttackFailure.SameFaction,
            WorldPvpRules.CheckAttack(Human, Undead with { RaceId = "HUMAN" }));
    }

    [Fact]
    public void CannotFarmAnotherCharacterOnSameAccount()
    {
        Assert.Equal(WorldPvpAttackFailure.SameAccount,
            WorldPvpRules.CheckAttack(Human, Undead with { AccountId = HumanAccount }));
    }

    [Fact]
    public void CannotAttackOwnCharacter()
    {
        Assert.Equal(WorldPvpAttackFailure.SameCharacter,
            WorldPvpRules.CheckAttack(Human, Human));
    }

    [Theory]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, true)]
    public void DeadOrTravellingCharactersAreProtected(
        bool attackerAlive, bool targetAlive, bool attackerTravelling, bool targetTravelling)
    {
        Assert.Equal(WorldPvpAttackFailure.ParticipantUnavailable,
            WorldPvpRules.CheckAttack(
                Human with { IsAlive = attackerAlive, IsTravelling = attackerTravelling },
                Undead with { IsAlive = targetAlive, IsTravelling = targetTravelling }));
    }

    [Fact]
    public void UnknownFactionAndEmptyIdentityFailClosed()
    {
        Assert.Equal(WorldPvpAttackFailure.InvalidParticipant,
            WorldPvpRules.CheckAttack(Human with { RaceId = "OTHER" }, Undead));
        Assert.Equal(WorldPvpAttackFailure.InvalidParticipant,
            WorldPvpRules.CheckAttack(Human with { CharacterId = Guid.Empty }, Undead));
        Assert.Equal(WorldPvpAttackFailure.InvalidParticipant,
            WorldPvpRules.CheckAttack(Human, Undead with { AccountId = Guid.Empty }));
    }
}
