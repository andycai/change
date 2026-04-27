using Change.Framework.Gas;

namespace Change.Runtime.Gas.Effects
{
    public sealed class TagEffect : ISkillEffect
    {
        private readonly bool _grant;
        private readonly SkillTag _tag;

        public TagEffect(bool grant, SkillTag tag)
        {
            _grant = grant;
            _tag = tag;
        }

        public void Execute(IAbilitySystem source, IAbilitySystem target)
        {
            if (_grant)
                target.Tags.AddTag(_tag);
            else
                target.Tags.RemoveTag(_tag);
        }
    }
}
