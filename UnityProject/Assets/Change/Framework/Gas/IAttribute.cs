namespace Change.Framework.Gas
{
    public interface IAttribute
    {
        string Name { get; }
        float BaseValue { get; set; }
        float CurrentValue { get; }
        void AddAdditive(float value);
        void RemoveAdditive(float value);
        void AddMultiplicative(float value);
        void RemoveMultiplicative(float value);
        void Recalculate();
    }
}
