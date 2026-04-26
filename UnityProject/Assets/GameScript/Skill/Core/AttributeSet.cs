using System;
using Change.Framework.Collections;
using Change.Framework.Skill;

namespace GameScript.Skill.Core
{
    public sealed class AttributeSet : IAttributeSet
    {
        private readonly FastDictionary<string, Attribute> _attributes;

        public event AttributeChangedHandler OnAttributeChanged;

        public AttributeSet(int capacity = 16)
        {
            _attributes = new FastDictionary<string, Attribute>(capacity: capacity);
        }

        public IAttribute GetAttribute(string name)
        {
            return _attributes.TryGetValue(name, out var attr) ? attr : null;
        }

        public float GetCurrentValue(string name)
        {
            return _attributes.TryGetValue(name, out var attr) ? attr.CurrentValue : 0f;
        }

        public void SetBaseValue(string name, float value)
        {
            if (!_attributes.TryGetValue(name, out var attr))
            {
                attr = new Attribute(name, value);
                _attributes.TryAdd(name, attr);
                OnAttributeChanged?.Invoke(name, 0f, value);
                return;
            }

            float oldValue = attr.CurrentValue;
            attr.BaseValue = value;
            OnAttributeChanged?.Invoke(name, oldValue, attr.CurrentValue);
        }

        public void ModifyCurrent(string name, float delta)
        {
            if (!_attributes.TryGetValue(name, out var attr))
            {
                attr = new Attribute(name, delta);
                _attributes.TryAdd(name, attr);
                return;
            }
            attr.BaseValue += delta;
        }

        internal Attribute GetOrCreateAttribute(string name, float baseValue = 0f)
        {
            if (!_attributes.TryGetValue(name, out var attr))
            {
                attr = new Attribute(name, baseValue);
                _attributes.TryAdd(name, attr);
            }
            return attr;
        }
    }
}
