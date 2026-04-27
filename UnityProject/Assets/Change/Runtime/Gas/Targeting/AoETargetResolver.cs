using System.Collections.Generic;
using Change.Framework.Gas;

namespace Change.Runtime.Gas.Targeting
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
            bool targetAllies = _targetType == TargetType.AllAllies;

            for (int i = 0; i < allEntities.Length; i++)
            {
                var entity = allEntities[i];
                if (targetEnemies && entity.TeamId != source.TeamId)
                    result.Add(entity);
                else if (targetAllies && entity.TeamId == source.TeamId && entity.EntityId != source.EntityId)
                    result.Add(entity);
            }
            return result.ToArray();
        }
    }
}
