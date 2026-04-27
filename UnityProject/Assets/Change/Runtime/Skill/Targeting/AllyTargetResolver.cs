using System.Collections.Generic;
using Change.Framework.Skill;

namespace Change.Runtime.Skill.Targeting
{
    public sealed class AllyTargetResolver : ITargetResolver
    {
        private readonly int _count;
        public TargetType Type => TargetType.Ally;
        public AllyTargetResolver(int count) { _count = count; }

        public IAbilitySystem[] Resolve(IAbilitySystem source, IAbilitySystem[] allEntities)
        {
            var allies = new List<IAbilitySystem>();
            for (int i = 0; i < allEntities.Length; i++)
            {
                if (allEntities[i].EntityId == source.EntityId)
                {
                    allies.Add(allEntities[i]);
                    break;
                }
            }
            return allies.ToArray();
        }
    }
}
