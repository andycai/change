using Change.Framework.Gas;
using Change.Runtime.Gas;

namespace GameScript.GasTemplate.Demo
{
    public static class DemoModifiers
    {
        public static Modifier CreateBurnModifier()
        {
            var config = new ModifierConfig
            {
                Id = "demo.burn",
                Polarity = ModifierPolarity.Debuff,
                GrantedTags = new[] { new GameplayTag("state.burning") },
                Stacking = ModifierStacking.Refresh,
                MaxStack = 1,
                Duration = 5f,
                TickInterval = 1f,
                TickEffects = new[] { DemoEffects.BurnTickDamage() }
            };
            return new Modifier(config);
        }
    }
}
