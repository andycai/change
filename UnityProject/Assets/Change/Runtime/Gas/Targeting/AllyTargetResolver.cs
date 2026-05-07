using System.Collections.Generic;
using System;
using Change.Framework.Gas;

namespace Change.Runtime.Gas.Targeting
{
    public sealed class AllyTargetResolver : ITargetResolver
    {
        private readonly int _count;
        private static readonly Random Rng = new Random();
        public TargetType Type => TargetType.Ally;
        public AllyTargetResolver(int count) { _count = count; }

        public IAbilitySystem[] Resolve(IAbilitySystem source, IAbilitySystem[] allEntities)
        {
            var allies = new List<IAbilitySystem>();
            for (int i = 0; i < allEntities.Length; i++)
            {
                if (allEntities[i].TeamId == source.TeamId && allEntities[i].EntityId != source.EntityId)
                    allies.Add(allEntities[i]);
            }
            int resultCount = Math.Min(_count, allies.Count);
            var result = new IAbilitySystem[resultCount];
            for (int i = 0; i < resultCount; i++)
            {
                int idx = Rng.Next(allies.Count);
                result[i] = allies[idx];
                allies.RemoveAt(idx);
            }

            return result;
        }
    }
}
