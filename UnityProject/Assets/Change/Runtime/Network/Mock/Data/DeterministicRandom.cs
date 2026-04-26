using System;

namespace Change.Runtime.Network
{
    public sealed class DeterministicRandom
    {
        private readonly Random _random;

        public DeterministicRandom(int seed)
        {
            _random = new Random(seed);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            return _random.Next(minInclusive, maxExclusive);
        }

        public float NextFloat(float minInclusive, float maxInclusive)
        {
            var unit = (float)_random.NextDouble();
            return minInclusive + ((maxInclusive - minInclusive) * unit);
        }

        public bool NextBool()
        {
            return _random.Next(0, 2) == 0;
        }
    }
}
