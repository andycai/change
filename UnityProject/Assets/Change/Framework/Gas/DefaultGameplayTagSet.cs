using System;
using System.Collections.Generic;

namespace Change.Framework.Gas
{
    public sealed class DefaultGameplayTagSet : IGameplayTagSet
    {
        private readonly Dictionary<GameplayTag, int> _counts = new Dictionary<GameplayTag, int>();

        public void AddTag(GameplayTag tag)
        {
            _counts.TryGetValue(tag, out var count);
            _counts[tag] = count + 1;
        }

        public void RemoveTag(GameplayTag tag)
        {
            if (!_counts.TryGetValue(tag, out var count) || count <= 0)
            {
                throw new InvalidOperationException($"Tag is not present: {tag}");
            }

            if (count == 1)
            {
                _counts.Remove(tag);
                return;
            }

            _counts[tag] = count - 1;
        }

        public bool HasTag(GameplayTag tag)
        {
            return GetTagCount(tag) > 0;
        }

        public int GetTagCount(GameplayTag tag)
        {
            return _counts.TryGetValue(tag, out var count) ? count : 0;
        }
    }
}
