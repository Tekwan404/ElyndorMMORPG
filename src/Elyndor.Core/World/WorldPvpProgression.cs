namespace Elyndor.Core.World;

public sealed record WorldPvpContribution(Guid CharacterId, decimal Damage, decimal Support);
public sealed record WorldPvpDefeat(Guid VictimId, DateTimeOffset AtUtc,
    IReadOnlyList<WorldPvpContribution> Contributions, Guid? KillerId = null);
public sealed record WorldPvpCredit(Guid CharacterId, bool Victory);

public static class WorldPvpBalance
{
    public static readonly DateTimeOffset SeasonEpoch = new(2026, 10, 17, 0, 0, 0, TimeSpan.Zero);
    public static readonly TimeSpan SupportWindow = TimeSpan.FromSeconds(30);
    public const int DailyRewardedOpponents = 20;
    public const int FieldHonorPerCredit = 20;
    public const int TokenHonorValue = 5;
    public const decimal MinimumContributionFraction = .1m;
    public static int Season(DateTimeOffset now) => Math.Max(1, 1 + (int)Math.Floor((now - SeasonEpoch).TotalDays / 56));
    public static DateTimeOffset SeasonEnds(int season) => SeasonEpoch.AddDays(season * 56d);
    public static int RepeatHonor(int fullAward, int previousCredits)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fullAward);
        ArgumentOutOfRangeException.ThrowIfNegative(previousCredits);
        return (int)((long)fullAward * Math.Max(0, 4 - previousCredits) / 4);
    }
    public static IReadOnlyList<WorldPvpHonorRank> Ranks { get; } = Array.AsReadOnly<WorldPvpHonorRank>([
        new(1, "Новобранец", 0), new(2, "Боец", 200), new(3, "Ветеран", 500),
        new(4, "Сержант", 1000), new(5, "Страж", 1800), new(6, "Рыцарь", 2800),
        new(7, "Командир", 4000), new(8, "Воевода", 5000), new(9, "Завоеватель", 8000),
        new(10, "Чемпион", 12000), new(11, "Генерал", 18000), new(12, "Маршал", 26000),
        new(13, "Полководец", 36000), new(14, "Верховный полководец", 50000),
    ]);
    public static WorldPvpHonorRank Rank(long honor) => Ranks.Last(r => r.RequiredHonor <= honor);
    public static int GroupAward(int award, int creditedCount, bool victory) => victory
        ? award : Math.Max(1, (award + Math.Max(1, creditedCount) - 1) / Math.Max(1, creditedCount));

    public static IReadOnlyList<WorldPvpCredit> Credits(WorldPvpDefeat defeat)
    {
        decimal total = defeat.Contributions.Sum(c => c.Damage + c.Support);
        if (total <= 0) return [];
        Guid winner = defeat.Contributions.OrderByDescending(c => c.Damage)
            .ThenBy(c => c.CharacterId).First().CharacterId;
        return defeat.Contributions.Where(c => c.Damage + c.Support >= total * MinimumContributionFraction)
            .Select(c => new WorldPvpCredit(c.CharacterId, c.CharacterId == winner)).ToArray();
    }
}

public sealed record WorldPvpHonorRank(int Level, string Name, long RequiredHonor);

public sealed class WorldPvpStanding
{
    private WorldPvpStanding() { }
    public WorldPvpStanding(Guid characterId, int season) { CharacterId = characterId; Season = season; }
    public Guid CharacterId { get; private set; }
    public int Season { get; private set; }
    public int Rating { get; private set; }
    public long SeasonHonor { get; private set; }
    public int Victories { get; private set; }
    public int Assists { get; private set; }
    public int Deaths { get; private set; }
    public int KillStreak { get; private set; }
    public void RecordDeath() { Deaths = checked(Deaths + 1); KillStreak = 0; }
    public void RecordKill() { Victories = checked(Victories + 1); KillStreak = checked(KillStreak + 1); }
    public void Credit(bool victory, long honor, bool countVictory = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(honor);
        SeasonHonor = checked(SeasonHonor + honor);
        if (victory) { if (countVictory) RecordKill(); }
        else Assists = checked(Assists + 1);
    }
}

public sealed class WorldPvpWallet
{
    private WorldPvpWallet() { }
    public WorldPvpWallet(Guid characterId) => CharacterId = characterId;
    public Guid CharacterId { get; private set; }
    public long FieldHonor { get; private set; }
    public long BloodyTokens { get; private set; }
    public void Grant(long honor, long tokens)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(honor);
        ArgumentOutOfRangeException.ThrowIfNegative(tokens);
        FieldHonor = checked(FieldHonor + honor);
        BloodyTokens = checked(BloodyTokens + tokens);
    }
    public bool TrySpend(long price, bool tokens)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price);
        if (tokens) { if (BloodyTokens < price) return false; BloodyTokens -= price; }
        else { if (FieldHonor < price) return false; FieldHonor -= price; }
        return true;
    }
}

// One immutable receipt per victim and battle, including rejected progression.
public sealed record WorldPvpDeathReceipt(Guid SessionId, Guid VictimId, DateTimeOffset AtUtc);
public sealed record WorldPvpRewardReceipt(Guid SessionId, Guid VictimId, Guid CharacterId,
    DateTimeOffset AtUtc, int Season, int Rating, long FieldHonor, long BloodyTokens, long GoldEarned = 0,
    int TargetDefeatNumber = 1, bool? Victory = null);
