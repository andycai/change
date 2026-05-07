using Change.Framework.Gas;

namespace Change.Runtime.Gas
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
                var entity = allEntities[i];
                var triggers = entity.GetTriggers(eventType);
                for (int t = 0; t < triggers.Count; t++)
                {
                    var trigger = triggers[t];
                    
                    // P2-5: Implement TriggerScope filtering
                    if (!IsScopeValid(trigger.Scope, entity, source, target))
                        continue;

                    trigger.TryFire(source, target, cascadeDepth);
                }
            }
        }

        private bool IsScopeValid(TriggerScope scope, IAbilitySystem owner, IAbilitySystem source, IAbilitySystem target)
        {
            switch (scope)
            {
                case TriggerScope.Self:
                    return owner == source || owner == target;
                case TriggerScope.Source:
                    return owner == source;
                case TriggerScope.Target:
                    return owner == target;
                case TriggerScope.AllEnemies:
                    if (source == null) return false;
                    return owner.TeamId != source.TeamId;
                case TriggerScope.AllAllies:
                    if (source == null) return false;
                    return owner.TeamId == source.TeamId;
                default:
                    return true;
            }
        }
    }
}
