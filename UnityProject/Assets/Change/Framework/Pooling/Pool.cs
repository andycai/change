using System;
using System.Collections.Generic;
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
using System.Runtime.CompilerServices;
#endif
using Fun.Framework.Pooling.Internal;

namespace Fun.Framework.Pooling
{
    public static class Pool<T> where T : class, IPoolable, new()
    {
        private static readonly Stack<T> Inactive = new Stack<T>(PoolDefaults.DefaultMaxSize);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly HashSet<T> Rented = new HashSet<T>(ReferenceEqualityComparer<T>.Instance);
#else
        private static readonly ConditionalWeakTable<T, LeaseState> LeaseStates = new ConditionalWeakTable<T, LeaseState>();
#endif
        private static int s_maxSize = PoolDefaults.DefaultMaxSize;

        private static long s_created;
        private static long s_rented;
        private static long s_released;
        private static long s_dropped;

        public static int InactiveCount => Inactive.Count;

        public static T Get()
        {
            s_rented++;

            if (Inactive.Count > 0)
            {
                var reused = Inactive.Pop();
                MarkRented(reused);
                return reused;
            }

            s_created++;
            var created = new T();
            MarkRented(created);
            return created;
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
            if (!Rented.Remove(item))
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
                s_created++;
                var created = new T();
                Inactive.Push(created);
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
        }

        public static PoolStats GetStats()
        {
            return new PoolStats(s_created, s_rented, s_released, s_dropped, s_maxSize, Inactive.Count);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void MarkRented(T item)
        {
            Rented.Add(item);
        }
#else
        private sealed class LeaseState
        {
            public bool IsRented;
        }

        private static void MarkRented(T item)
        {
            if (!LeaseStates.TryGetValue(item, out var state))
            {
                state = new LeaseState();
                LeaseStates.Add(item, state);
            }

            state.IsRented = true;
        }

        private static bool TryMarkReleased(T item)
        {
            if (!LeaseStates.TryGetValue(item, out var state) || !state.IsRented)
            {
                return false;
            }

            state.IsRented = false;
            return true;
        }
#endif
    }
}
