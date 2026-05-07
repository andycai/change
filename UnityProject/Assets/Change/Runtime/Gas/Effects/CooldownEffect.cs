using Change.Framework.Gas;

namespace Change.Runtime.Gas.Effects
{
    public sealed class CooldownEffect : IGameplayEffect
    {
        private readonly float _duration;

        public CooldownEffect(float duration)
        {
            _duration = duration;
        }

        public void Execute(in EffectContext context)
        {
            if (context.Ability is GameplayAbility ability)
                ability.StartCooldown(_duration);
        }
    }
}
