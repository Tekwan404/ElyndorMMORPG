using Elyndor.Core.Balance;
using Elyndor.Core.Combat;
using Elyndor.Core.Content;
using Elyndor.Core.Monsters;

namespace Elyndor.UnitTests.Content;

public sealed class CombatBalanceProfileTests
{
    [Fact]
    public void StandardLevelTwentyOneTargetMatchesAuthoredWorldEnvelope()
    {
        CombatBalanceProfile profile = Profile();

        MonsterStatTarget target = MonsterStatCurve.Resolve(
            profile,
            level: 21,
            MonsterRank.Normal,
            "STANDARD");

        Assert.Equal(2320, target.MaxHp);
        Assert.Equal(102, target.AttackPower);
        Assert.Equal(88, target.Armor);
        Assert.Equal(44, target.MagicResistance);
        Assert.Equal(70, target.AutoAttackBaseDamage);
    }

    [Fact]
    public void AuditReportsDeviationWithoutMutatingMonster()
    {
        CombatBalanceProfile profile = Profile();
        MonsterDefinition monster = new(
            "ASHEN_BORDER_OBUGLENNYI_DREVEN_L21",
            "Обугленный древень",
            MonsterRank.Normal,
            21,
            2400,
            new CombatStats(
                21,
                97,
                4,
                7,
                1,
                95,
                43,
                0.005m,
                0.005m,
                110,
                0),
            TimeSpan.FromSeconds(2.15),
            72,
            [],
            "AUTHORED_EMPTY_AI",
            AutoAttackAttackPowerCoefficient: 0.5m,
            AutoAttackBaseDamageMin: 65,
            AutoAttackBaseDamageMax: 79);

        MonsterBalanceAuditEntry result = MonsterBalanceAudit.Audit(profile, monster);

        Assert.Equal(2400, monster.MaxHp);
        Assert.Equal(2320, result.Target.MaxHp);
        Assert.InRange(result.HpDeltaPercent, 3m, 4m);
        Assert.True(result.WithinTolerance);
    }

    private static CombatBalanceProfile Profile() => new(
        "TEST",
        40,
        [
            new("WEAK", 6, "NORMAL"),
            new("NORMAL", 2, "ELITE"),
            new("GOOD", 0, "BOSS")
        ],
        new MonsterStatCurveDefinition(
            new CombatCurveCoefficients(100, 18, 4.65m),
            new CombatCurveCoefficients(8, 4.7m, 0),
            new CombatCurveCoefficients(8, 3, 0.05m),
            new CombatCurveCoefficients(4, 1.6m, 0.02m),
            new CombatCurveCoefficients(5.5m, 2.4m, 0.04m)),
        [
            new(MonsterRank.Normal, 1, 1, 1, 1, 1),
            new(MonsterRank.Elite, 1.6m, 1.15m, 1.15m, 1.15m, 1.1m)
        ],
        [
            new("STANDARD", 1, 1, 1, 1, 1),
            new("TANK", 1.15m, 0.95m, 1.25m, 1.1m, 0.95m)
        ],
        new CombatSecondsRange(8, 12),
        new CombatSecondsRange(14, 24),
        new CombatSecondsRange(16, 24),
        new CombatSecondsRange(12, 22),
        25);
}
