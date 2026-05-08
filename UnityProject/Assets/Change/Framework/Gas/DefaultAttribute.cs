using System;

namespace Change.Framework.Gas
{
    public sealed class DefaultAttribute : IAttribute
    {
        private float _additiveTotal;
        private float _multiplicativeTotal;

        public DefaultAttribute(string name, float baseValue)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Attribute name cannot be null or empty.", nameof(name));
            }

            Name = name;
            BaseValue = baseValue;
            Recalculate();
        }

        public string Name { get; }

        public float BaseValue { get; set; }

        public float CurrentValue { get; private set; }

        public void AddAdditive(float value)
        {
            _additiveTotal += value;
        }

        public void RemoveAdditive(float value)
        {
            _additiveTotal -= value;
        }

        public void AddMultiplicative(float value)
        {
            _multiplicativeTotal += value;
        }

        public void RemoveMultiplicative(float value)
        {
            _multiplicativeTotal -= value;
        }

        public void Recalculate()
        {
            CurrentValue = (BaseValue + _additiveTotal) * (1f + _multiplicativeTotal);
        }
    }
}
