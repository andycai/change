using Change.Framework.Gas;

namespace Change.Runtime.Gas.Effects
{
    public sealed class TagEffect : IGameplayEffect
    {
        private readonly GameplayTag _tag;
        private readonly bool _isAdd;

        public TagEffect(GameplayTag tag, bool isAdd = true)
        {
            _tag = tag;
            _isAdd = isAdd;
        }

        public void Execute(IAbilitySystem source, IAbilitySystem target)
        {
            if (_isAdd)
                target.Tags.AddTag(_tag);
            else
                target.Tags.RemoveTag(_tag);
        }
    }
}
