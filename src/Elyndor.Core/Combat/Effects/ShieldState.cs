namespace Elyndor.Core.Combat.Effects;

public sealed class ShieldState
{
    private readonly Guid _sourceActorId;
    private readonly DateTimeOffset _expiresAtUtc;
    private decimal _remainingAmount;

    public ShieldState(Guid sourceActorId, decimal amount, DateTimeOffset expiresAtUtc)
    {
        if (sourceActorId == Guid.Empty)
            throw new ArgumentException("Shield source is required.", nameof(sourceActorId));
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        _sourceActorId = sourceActorId;
        _remainingAmount = amount;
        _expiresAtUtc = expiresAtUtc;
    }

    public Guid SourceActorId => _sourceActorId;
    public decimal RemainingAmount => _remainingAmount;

    public ShieldAbsorption Absorb(decimal incomingDamage, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(incomingDamage);
        if (incomingDamage == 0 || now >= _expiresAtUtc || _remainingAmount == 0)
            return new(0, incomingDamage);

        decimal absorbed = Math.Min(incomingDamage, _remainingAmount);
        _remainingAmount -= absorbed;
        return new(absorbed, incomingDamage - absorbed);
    }
}

public sealed record ShieldAbsorption(decimal Absorbed, decimal RemainingDamage);
