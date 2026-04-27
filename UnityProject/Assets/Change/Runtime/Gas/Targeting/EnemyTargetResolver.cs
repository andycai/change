using System;
using Change.Framework.Gas;

namespace Change.Runtime.Gas.Targeting
{
    public sealed class EnemyTargetResolver : ITargetResolver
    {
        private readonly int _count;
        private static readonly Random _rng = new Random();

        public TargetType Type => TargetType.Enemy;

        public EnemyTargetResolver(int count) { _count = count; }

        public IAbilitySystem[] Resolve(IAbilitySystem source, IAbilitySystem[] allEntities)
        {
            var enemies = new System.Collections.Generic.List<IAbilitySystem>();
            for (int i = 0; i < allEntities.Length; i++)
            {
                if (allEntities[i].TeamId != source.TeamId)
                    enemies.Add(allEntities[i]);
            }
            int resultCount = Math.Min(_count, enemies.Count);
            var result = new IAbilitySystem[resultCount];
            for (int i = 0; i < resultCount; i++)
            {
                int idx = _rng.Next(enemies.Count);
                result[i] = enemies[idx];
                enemies.RemoveAt(idx);
            }
            return result;
        }
    }
}
