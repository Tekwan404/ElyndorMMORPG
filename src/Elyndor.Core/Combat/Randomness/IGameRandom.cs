namespace Elyndor.Core.Combat.Randomness;

public interface IGameRandom
{
    decimal NextUnit();
}

public interface IGameRandomFactory
{
    IGameRandom Create();
}

public sealed class SystemGameRandomFactory : IGameRandomFactory
{
    public IGameRandom Create() => new SystemGameRandom(Random.Shared.Next());

    private sealed class SystemGameRandom(int seed) : IGameRandom
    {
        private readonly Random _random = new(seed);

        public decimal NextUnit() => (decimal)_random.NextDouble();
    }
}

public sealed class SequenceGameRandom(params decimal[] values) : IGameRandom
{
    private int _index;

    public decimal NextUnit()
    {
        if (_index >= values.Length)
        {
            throw new InvalidOperationException("The deterministic RNG sequence is exhausted.");
        }

        decimal value = values[_index++];
        if (value is < 0 or >= 1)
        {
            throw new InvalidOperationException("RNG values must be in the range [0, 1). ");
        }

        return value;
    }
}
