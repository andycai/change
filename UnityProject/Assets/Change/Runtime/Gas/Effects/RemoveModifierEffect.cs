using Change.Framework.Gas;

namespace Change.Runtime.Gas.Effects
{
    public sealed class RemoveModifierEffect : IGameplayEffect
    {
        private readonly string _modifierId;
        private readonly GameplayTag? _tag;

        public RemoveModifierEffect(string modifierId)
        {
            _modifierId = modifierId;
        }

        public RemoveModifierEffect(GameplayTag tag)
        {
            _tag = tag;
        }

        public void Execute(IAbilitySystem source, IAbilitySystem target)
        {
            if (_tag.HasValue)
                target.RemoveModifierByTag(_tag.Value);
            else if (!string.IsNullOrEmpty(_modifierId))
                target.RemoveModifier(_modifierId);
        }
    }
}
