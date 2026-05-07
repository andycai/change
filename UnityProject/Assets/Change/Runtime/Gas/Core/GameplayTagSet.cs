using System.Collections.Generic;
using Change.Framework.Gas;

namespace Change.Runtime.Gas
{
    public sealed class GameplayTagSet : IGameplayTagSet
    {
        private static readonly Dictionary<string, GameplayTag[]> HierarchyCache = new Dictionary<string, GameplayTag[]>();
        private readonly Dictionary<int, int> _tagCounts;
        private readonly Dictionary<int, GameplayTag> _tagMap;

        public GameplayTagSet(int capacity = 16)
        {
            _tagCounts = new Dictionary<int, int>(capacity);
            _tagMap = new Dictionary<int, GameplayTag>(capacity);
        }

        public void AddTag(GameplayTag tag)
        {
            var hierarchy = GetHierarchy(tag);
            for (int i = 0; i < hierarchy.Length; i++)
            {
                GameplayTag subTag = hierarchy[i];
                int hash = subTag.Hash;

                _tagMap[hash] = subTag;
                if (_tagCounts.TryGetValue(hash, out int count))
                    _tagCounts[hash] = count + 1;
                else
                    _tagCounts[hash] = 1;
            }
        }

        public void RemoveTag(GameplayTag tag)
        {
            var hierarchy = GetHierarchy(tag);
            for (int i = 0; i < hierarchy.Length; i++)
            {
                GameplayTag subTag = hierarchy[i];
                int hash = subTag.Hash;

                if (_tagCounts.TryGetValue(hash, out int count))
                {
                    if (count <= 1)
                    {
                        _tagCounts.Remove(hash);
                        _tagMap.Remove(hash);
                    }
                    else
                    {
                        _tagCounts[hash] = count - 1;
                    }
                }
            }
        }

        private static GameplayTag[] GetHierarchy(GameplayTag tag)
        {
            if (HierarchyCache.TryGetValue(tag.Value, out var cached))
                return cached;

            var list = new List<GameplayTag>(4);
            string value = tag.Value;
            int lastDotIndex = value.Length;
            while (lastDotIndex > 0)
            {
                list.Add(new GameplayTag(value.Substring(0, lastDotIndex)));
                lastDotIndex = value.LastIndexOf('.', lastDotIndex - 1);
            }

            cached = list.ToArray();
            HierarchyCache[tag.Value] = cached;
            return cached;
        }

        public bool HasTag(GameplayTag tag)
        {
            return _tagCounts.ContainsKey(tag.Hash);
        }

        public int GetTagCount(GameplayTag tag)
        {
            return _tagCounts.TryGetValue(tag.Hash, out int count) ? count : 0;
        }

        public void Clear()
        {
            _tagCounts.Clear();
            _tagMap.Clear();
        }

        public int Count => _tagMap.Count;
    }
}
