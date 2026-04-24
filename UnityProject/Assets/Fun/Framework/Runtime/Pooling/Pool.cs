using System;
using System.Collections.Generic;
using Fun.Framework.Pooling.Internal;

namespace Fun.Framework.Pooling
{
    public static class Pool<T> where T : class, IPoolable, new()
    {
        private static readonly Stack<T> Inactive = new Stack<T>(PoolDefaults.DefaultMaxSize);
        private static int s_maxSize = PoolDefaults.DefaultMaxSize;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly HashSet<T> Known = new HashSet<T>(ReferenceEqualityComparer<T>.Instance);
        private static readonly HashSet<T> Rented = new HashSet<T>(ReferenceEqualityComparer<T>.Instance);
#endif

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Rented.Add(reused);
#endif
                return reused;
            }

            s_created++;
            var created = new T();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Known.Add(created);
            Rented.Add(created);
#endif
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
            if (!Known.Contains(item))
            {
                throw new InvalidOperationException($"Cannot release unknown instance of {typeof(T).FullName}.");
            }

            if (!Rented.Contains(item))
            {
                throw new InvalidOperationException($"Cannot release instance of {typeof(T).FullName} that is not rented.");
            }
#endif

            s_released++;
            item.Reset();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Rented.Remove(item);
#endif

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Known.Add(created);
#endif
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
    }
}
