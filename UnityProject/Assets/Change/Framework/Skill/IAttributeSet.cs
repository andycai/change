using System;

namespace Change.Framework.Skill
{
    public delegate void AttributeChangedHandler(string attributeName, float oldValue, float newValue);

    public interface IAttributeSet
    {
        IAttribute GetAttribute(string name);
        float GetCurrentValue(string name);
        void SetBaseValue(string name, float value);
        event AttributeChangedHandler OnAttributeChanged;
    }
}
