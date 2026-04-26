using System.Collections.Generic;
using Change.Framework.Skill;

namespace GameScript.Skill.Core
{
    public sealed class TriggerEngine
    {
        private readonly int _maxCascadeDepth;

        public TriggerEngine(int maxCascadeDepth = 5)
        {
            _maxCascadeDepth = maxCascadeDepth;
        }

        public void DispatchEvent(
            TriggerEventType eventType,
            IAbilitySystem source,
            IAbilitySystem target,
            IAbilitySystem[] allEntities,
            int cascadeDepth = 0)
        {
            if (cascadeDepth >= _maxCascadeDepth)
                return;

            for (int i = 0; i < allEntities.Length; i++)
            {
                var entity = allEntities[i] as AbilitySystem;
                if (entity == null) continue;

                var triggers = entity.GetTriggers();
                for (int t = 0; t < triggers.Count; t++)
                {
                    var trigger = triggers[t];
                    if (trigger.EventType != eventType)
                        continue;

                    if (trigger.TryFire(source, target, cascadeDepth))
                    {
                        DispatchEvent(eventType, source, target, allEntities, cascadeDepth + 1);
                    }
                }
            }
        }
    }
}
