namespace Elyndor.Core.World;

// Ordinary money, in integer bronze units (10,000 bronze = 1 gold).
public sealed record WorldPvpRewardProfile(long GoldPerKill = 0, long GoldPerAssist = 0)
{
    public const long MaximumReward = 1_000_000_000;
    public bool IsValid() => GoldPerKill is >= 0 and <= MaximumReward
        && GoldPerAssist is >= 0 and <= MaximumReward;
}
