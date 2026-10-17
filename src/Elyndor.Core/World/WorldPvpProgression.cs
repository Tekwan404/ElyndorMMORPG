namespace Elyndor.Core.World;

public sealed record WorldPvpContribution(Guid CharacterId, decimal Damage, decimal Support);
public sealed record WorldPvpDefeat(Guid VictimId, DateTimeOffset AtUtc,
    IReadOnlyList<WorldPvpContribution> Contributions, Guid? KillerId = null);
public sealed record WorldPvpCredit(Guid CharacterId, bool Victory);

public static class WorldPvpBalance
{
    public static readonly DateTimeOffset SeasonEpoch = new(2026, 10, 17, 0, 0, 0, TimeSpan.Zero);
    public static readonly TimeSpan PairCooldown = TimeSpan.FromDays(1);
    public static readonly TimeSpan MinimumFightDuration = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan SupportWindow = TimeSpan.FromSeconds(30);
    public const int DailyRewardedOpponents = 20;
    public const int FieldHonorPerCredit = 5;
    public const int TokenHonorValue = 5;
    public const decimal MinimumDamageFraction = .2m;
    public const decimal MinimumContributionFraction = .1m;
    public static int Season(DateTimeOffset now) => Math.Max(1, 1 + (int)Math.Floor((now - SeasonEpoch).TotalDays / 56));
    public static DateTimeOffset SeasonEnds(int season) => SeasonEpoch.AddDays(season * 56d);
    public static int RatingAward(int own, int enemy) => Math.Clamp(10 + (enemy - own) / 100, 5, 15);

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

public sealed class WorldPvpStanding
{
    private WorldPvpStanding() { }
    public WorldPvpStanding(Guid characterId, int season) { CharacterId = characterId; Season = season; }
    public Guid CharacterId { get; private set; }
    public int Season { get; private set; }
    public int Rating { get; private set; }
    public int Victories { get; private set; }
    public int Assists { get; private set; }
    public int Deaths { get; private set; }
    public void RecordDeath() => Deaths = checked(Deaths + 1);
    public void RecordKill() => Victories = checked(Victories + 1);
    public void Credit(bool victory, int rating, bool countVictory = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rating);
        Rating = checked(Rating + rating);
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
    DateTimeOffset AtUtc, int Season, int Rating, long FieldHonor, long BloodyTokens);
