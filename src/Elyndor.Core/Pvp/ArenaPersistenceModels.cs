namespace Elyndor.Core.Pvp;

public sealed class ArenaMatch
{
    private ArenaMatch() { }

    public ArenaMatch(Guid id, Guid characterAId, Guid characterBId, DateTimeOffset startedAtUtc,
        string seasonId = ArenaSeason.CurrentId)
    {
        if (id == Guid.Empty || characterAId == Guid.Empty || characterBId == Guid.Empty
            || characterAId == characterBId) throw new ArgumentException("Arena participants must be distinct.");
        if (startedAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Arena time must be UTC.");
        Id = id;
        CharacterAId = characterAId;
        CharacterBId = characterBId;
        StartedAtUtc = startedAtUtc;
        SeasonId = seasonId;
    }

    public Guid Id { get; private set; }
    public Guid CharacterAId { get; private set; }
    public Guid CharacterBId { get; private set; }
    public string SeasonId { get; private set; } = null!;
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public DateTimeOffset? SettledAtUtc { get; private set; }
    public ArenaMatchOutcome Outcome { get; private set; } = ArenaMatchOutcome.Active;
    public bool EligibleForProgression { get; private set; } = true;

    public void Complete(ArenaMatchOutcome outcome, DateTimeOffset now, bool eligibleForProgression = true)
    {
        if (outcome == ArenaMatchOutcome.Active) throw new ArgumentException("Match outcome must be terminal.");
        if (now.Offset != TimeSpan.Zero || now < StartedAtUtc) throw new ArgumentException("Completion time must be UTC and after start.");
        if (Outcome != ArenaMatchOutcome.Active) return;
        Outcome = outcome;
        CompletedAtUtc = now;
        EligibleForProgression = eligibleForProgression;
    }

    public void MarkSettled(DateTimeOffset now)
    {
        if (Outcome == ArenaMatchOutcome.Active || SettledAtUtc is not null) throw new InvalidOperationException("Match cannot be settled twice or while active.");
        SettledAtUtc = now;
    }
}

public static class ArenaSeason
{
    public const string CurrentId = "S1";
}

public sealed class ArenaStanding
{
    private ArenaStanding() { }
    public ArenaStanding(Guid characterId, string seasonId)
    {
        CharacterId = characterId;
        SeasonId = seasonId;
        Rating = ArenaProgressionRules.InitialRating;
    }

    public Guid CharacterId { get; private set; }
    public string SeasonId { get; private set; } = null!;
    public int Rating { get; private set; }
    public int Wins { get; private set; }
    public int Losses { get; private set; }
    public int Draws { get; private set; }

    public void Record(ArenaMatchOutcome outcome, int rating, bool isFirst)
    {
        if (outcome == ArenaMatchOutcome.Cancelled) return;
        Rating = rating;
        if (outcome == ArenaMatchOutcome.Draw) Draws++;
        else if (outcome == (isFirst ? ArenaMatchOutcome.WinnerA : ArenaMatchOutcome.WinnerB)) Wins++;
        else Losses++;
    }
}

public sealed class ArenaHonorWallet
{
    private ArenaHonorWallet() { }
    public ArenaHonorWallet(Guid characterId) => CharacterId = characterId;
    public Guid CharacterId { get; private set; }
    public long Balance { get; private set; }
    public void Grant(long amount) => Balance = checked(Balance + (amount > 0 ? amount : throw new ArgumentOutOfRangeException(nameof(amount))));
}

public sealed class ArenaHonorLedgerEntry
{
    private ArenaHonorLedgerEntry() { }
    public ArenaHonorLedgerEntry(Guid matchId, Guid characterId, long delta, long balanceAfter, DateTimeOffset createdAtUtc)
    {
        if (matchId == Guid.Empty || characterId == Guid.Empty || delta <= 0 || balanceAfter < delta)
            throw new ArgumentException("Invalid Honor grant.");
        MatchId = matchId;
        CharacterId = characterId;
        Delta = delta;
        BalanceAfter = balanceAfter;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid MatchId { get; private set; }
    public Guid CharacterId { get; private set; }
    public long Delta { get; private set; }
    public long BalanceAfter { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
