using System;
using System.Collections.Generic;

namespace Change.Framework.Gas
{
    public sealed class DefaultAttributeSet : IAttributeSet
    {
        private readonly Dictionary<string, IAttribute> _attributes;

        public DefaultAttributeSet(params IAttribute[] attributes)
        {
            _attributes = new Dictionary<string, IAttribute>(StringComparer.Ordinal);

            if (attributes == null)
            {
                throw new ArgumentNullException(nameof(attributes));
            }

            for (var i = 0; i < attributes.Length; i++)
            {
                var attribute = attributes[i] ?? throw new ArgumentNullException(nameof(attributes), "Attribute entry cannot be null.");
                if (_attributes.ContainsKey(attribute.Name))
                {
                    throw new InvalidOperationException($"Duplicate attribute registration: {attribute.Name}");
                }

                _attributes.Add(attribute.Name, attribute);
            }
        }

        public event AttributeChangedHandler OnAttributeChanged;

        public IAttribute GetAttribute(string name)
        {
            if (!_attributes.TryGetValue(name, out var attribute))
            {
                throw new InvalidOperationException($"Attribute is not registered: {name}");
            }

            return attribute;
        }

        public float GetCurrentValue(string name)
        {
            return GetAttribute(name).CurrentValue;
        }

        public void SetBaseValue(string name, float value)
        {
            var attribute = GetAttribute(name);
            var oldValue = attribute.CurrentValue;
            attribute.BaseValue = value;
            attribute.Recalculate();
            RaiseChanged(name, oldValue, attribute.CurrentValue);
        }

        public void ModifyCurrent(string name, float delta)
        {
            var attribute = GetAttribute(name);
            var oldValue = attribute.CurrentValue;
            attribute.BaseValue += delta;
            attribute.Recalculate();
            RaiseChanged(name, oldValue, attribute.CurrentValue);
        }

        private void RaiseChanged(string name, float oldValue, float newValue)
        {
            if (oldValue == newValue)
            {
                return;
            }

            OnAttributeChanged?.Invoke(name, oldValue, newValue);
        }
    }
}
