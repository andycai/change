using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Change.Framework.Pooling
{
    public static class Pool<T> where T : class, IPoolable, new()
    {
        private static readonly Stack<T> Inactive = new Stack<T>(PoolDefaults.DefaultMaxSize);
        private static ConditionalWeakTable<T, LeaseState> s_leaseStates = new ConditionalWeakTable<T, LeaseState>();
        private static int s_maxSize = PoolDefaults.DefaultMaxSize;

        private static long s_created;
        private static long s_rented;
        private static long s_released;
        private static long s_dropped;

        public static int InactiveCount => Inactive.Count;

        public static T Get()
        {
            T item;
            if (Inactive.Count > 0)
            {
                item = Inactive.Pop();
            }
            else
            {
                item = new T();
                s_created++;
            }

            MarkRented(item);
            s_rented++;
            return item;
        }

        public static void Release(T item)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }
#else
            if (item == null)
            {
                return;
            }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!TryMarkReleased(item))
            {
                throw new InvalidOperationException($"Cannot release instance of {typeof(T).FullName} that is not currently rented by this pool.");
            }
#else
            if (!TryMarkReleased(item))
            {
                return;
            }
#endif

            s_released++;
            item.Reset();

            if (Inactive.Count >= s_maxSize)
            {
                s_dropped++;
                return;
            }

            Inactive.Push(item);
        }

        public static void Prewarm(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var target = Math.Min(count, s_maxSize);
            while (Inactive.Count < target)
            {
                var created = new T();
                Inactive.Push(created);
                s_created++;
            }
        }

        public static void SetMaxSize(int maxSize)
        {
            if (maxSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSize));
            }

            s_maxSize = maxSize;
            while (Inactive.Count > s_maxSize)
            {
                Inactive.Pop();
            }
        }

        public static void Clear()
        {
            Inactive.Clear();
            s_leaseStates = new ConditionalWeakTable<T, LeaseState>();
        }

        public static PoolStats GetStats()
        {
            return new PoolStats(s_created, s_rented, s_released, s_dropped, s_maxSize, Inactive.Count);
        }

        private sealed class LeaseState
        {
            public bool IsRented;
        }

        private static void MarkRented(T item)
        {
            if (!s_leaseStates.TryGetValue(item, out var state))
            {
                state = new LeaseState();
                s_leaseStates.Add(item, state);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (state.IsRented)
            {
                throw new InvalidOperationException($"Cannot rent instance of {typeof(T).FullName} because it is already marked as rented.");
            }
#endif

            state.IsRented = true;
        }

        private static bool TryMarkReleased(T item)
        {
            if (!s_leaseStates.TryGetValue(item, out var state) || !state.IsRented)
            {
                return false;
            }

            state.IsRented = false;
            return true;
        }
    }
}
