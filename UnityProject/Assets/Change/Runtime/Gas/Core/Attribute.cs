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
        private bool _isDirty = true;

        public string Name { get; }

        public float BaseValue
        {
            get => _baseValue;
            set
            {
                if (System.Math.Abs(_baseValue - value) < float.Epsilon) return;
                _baseValue = value;
                MarkDirty();
            }
        }

        public float CurrentValue
        {
            get
            {
                if (_isDirty) Recalculate();
                return _currentValue;
            }
        }

        public Attribute(string name, float baseValue = 0f)
        {
            Name = name;
            _baseValue = baseValue;
            _additiveSum = 0f;
            _multiplicativeProduct = 1f;
            _currentDelta = 0f;
            _currentValue = baseValue;
            _isDirty = true;
        }

        public void AddAdditive(float value)
        {
            _additiveSum += value;
            MarkDirty();
        }

        public void RemoveAdditive(float value)
        {
            _additiveSum -= value;
            MarkDirty();
        }

        public void AddMultiplicative(float value)
        {
            _multiplicativeProduct *= value;
            MarkDirty();
        }

        public void RemoveMultiplicative(float value)
        {
            _multiplicativeProduct /= value;
            MarkDirty();
        }

        public void ModifyCurrentDelta(float delta)
        {
            _currentDelta += delta;
            MarkDirty();
        }

        private void MarkDirty() => _isDirty = true;

        public void Recalculate()
        {
            _currentValue = (_baseValue + _additiveSum) * _multiplicativeProduct + _currentDelta;
            _isDirty = false;
        }
    }
}
