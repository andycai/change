using System.Collections.Generic;

namespace Change.Framework.Collections
{
    public sealed class FastHashSet<T> : IClearable
    {
        private readonly FastDictionary<T, byte> _map;

        public FastHashSet(int capacity = 4, IEqualityComparer<T> comparer = null)
        {
            _map = new FastDictionary<T, byte>(capacity, comparer);
        }

        public int Count => _map.Count;

        public bool Add(T value)
        {
            return _map.TryAdd(value, 1);
        }

        public bool AddNoResize(T value)
        {
            return _map.TryAddNoResize(value, 1);
        }

        public bool Contains(T value)
        {
            return _map.ContainsKey(value);
        }

        public bool Remove(T value)
        {
            return _map.Remove(value);
        }

        public void Clear(ClearMode mode = ClearMode.Logical)
        {
            _map.Clear(mode);
        }
    }
}
