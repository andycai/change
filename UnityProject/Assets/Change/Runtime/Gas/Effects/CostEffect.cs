using Change.Framework.Gas;

namespace Change.Runtime.Gas.Effects
{
    public sealed class CostEffect : IGameplayEffect
    {
        private readonly string _attribute;
        private readonly float _amount;

        public CostEffect(string attribute, float amount)
        {
            _attribute = attribute;
            _amount = amount;
        }

        public void Execute(in EffectContext context)
        {
            context.Source.Attributes.ModifyCurrent(_attribute, -_amount);
        }
    }
}
