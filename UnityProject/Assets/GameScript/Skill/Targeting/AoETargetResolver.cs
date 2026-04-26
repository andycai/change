using System.Collections.Generic;
using Change.Framework.Skill;

namespace GameScript.Skill.Targeting
{
    public sealed class AoETargetResolver : ITargetResolver
    {
        private readonly TargetType _targetType;
        public TargetType Type => _targetType;
        public AoETargetResolver(TargetType targetType) { _targetType = targetType; }

        public IAbilitySystem[] Resolve(IAbilitySystem source, IAbilitySystem[] allEntities)
        {
            var result = new List<IAbilitySystem>();
            bool targetEnemies = _targetType == TargetType.AllEnemies;
            for (int i = 0; i < allEntities.Length; i++)
            {
                bool isEnemy = allEntities[i].EntityId != source.EntityId;
                if (targetEnemies && isEnemy) result.Add(allEntities[i]);
                else if (!targetEnemies && !isEnemy) result.Add(allEntities[i]);
            }
            return result.ToArray();
        }
    }
}
