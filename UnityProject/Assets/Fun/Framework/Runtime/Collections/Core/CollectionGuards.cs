using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Fun.Framework.Tests")]

namespace Fun.Framework.Collections
{
    internal static class CollectionGuards
    {
        public static void ThrowIfNegativeCapacity(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be >= 0.");
            }
        }

        public static void ThrowIfIndexOutOfRange(int index, int count)
        {
            if ((uint)index >= (uint)count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"Index {index} out of range. Count={count}.");
            }
        }

        public static void ThrowIfNull(object value, string paramName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(paramName);
            }
        }
    }
}
