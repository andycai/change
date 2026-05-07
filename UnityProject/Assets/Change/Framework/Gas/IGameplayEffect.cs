namespace Change.Framework.Gas
{
    public interface IGameplayEffect
    {
        void Execute(in EffectContext context);
    }
}
