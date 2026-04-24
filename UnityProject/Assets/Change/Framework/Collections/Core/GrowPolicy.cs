using System;

namespace Change.Framework.Collections
{
    internal static class GrowPolicy
    {
        public static int Next(int currentCapacity, int minimum)
        {
            CollectionGuards.ThrowIfNegativeCapacity(currentCapacity);
            CollectionGuards.ThrowIfNegativeCapacity(minimum);

            var next = currentCapacity == 0 ? 4 : currentCapacity * 2;
            if (next < minimum)
            {
                next = minimum;
            }

            if (next < 0)
            {
                next = int.MaxValue;
            }

            return Math.Max(next, 4);
        }
    }
}
