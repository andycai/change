using System;

namespace Change.Runtime.Network
{
    /// <summary>
    /// A simple deterministic random number generator for 0GC.
    /// Using a simple Linear Congruential Generator (LCG).
    /// </summary>
    public sealed class DeterministicRandom
    {
        private uint _state;

        public DeterministicRandom(int seed)
        {
            Reset(seed);
        }

        public void Reset(int seed)
        {
            _state = (uint)seed;
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            }

            // LCG parameters from glibc
            _state = (1103515245 * _state + 12345) & 0x7fffffff;
            
            var range = maxExclusive - minInclusive;
            return minInclusive + (int)(_state % (uint)range);
        }

        public float NextFloat(float minInclusive, float maxInclusive)
        {
            if (maxInclusive < minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxInclusive));
            }

            _state = (1103515245 * _state + 12345) & 0x7fffffff;
            var unit = _state / (float)0x7fffffff;
            return minInclusive + ((maxInclusive - minInclusive) * unit);
        }

        public bool NextBool()
        {
            return NextInt(0, 2) == 0;
        }
    }
}
