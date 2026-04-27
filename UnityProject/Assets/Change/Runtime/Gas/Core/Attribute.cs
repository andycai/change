using Change.Framework.Gas;

namespace Change.Runtime.Gas
{
    public sealed class Attribute : IAttribute
    {
        private float _baseValue;
        private float _additiveSum;
        private float _multiplicativeProduct = 1f;
        private float _currentDelta;
        private float _currentValue;

        public string Name { get; }

        public float BaseValue
        {
            get => _baseValue;
            set
            {
                _baseValue = value;
                Recalculate();
            }
        }

        public float CurrentValue => _currentValue;

        public Attribute(string name, float baseValue = 0f)
        {
            Name = name;
            _baseValue = baseValue;
            _additiveSum = 0f;
            _multiplicativeProduct = 1f;
            _currentDelta = 0f;
            _currentValue = baseValue;
        }

        public void AddAdditive(float value)
        {
            _additiveSum += value;
            Recalculate();
        }

        public void RemoveAdditive(float value)
        {
            _additiveSum -= value;
            Recalculate();
        }

        public void AddMultiplicative(float value)
        {
            _multiplicativeProduct *= value;
            Recalculate();
        }

        public void RemoveMultiplicative(float value)
        {
            _multiplicativeProduct /= value;
            Recalculate();
        }

        public void ModifyCurrentDelta(float delta)
        {
            _currentDelta += delta;
            Recalculate();
        }

        public void Recalculate()
        {
            _currentValue = (_baseValue + _additiveSum) * _multiplicativeProduct + _currentDelta;
        }
    }
}
