using System;

namespace Change.Framework.Gas
{
    public delegate void AttributeChangedHandler(string attributeName, float oldValue, float newValue);

    public interface IAttributeSet
    {
        IAttribute GetAttribute(string name);
        float GetCurrentValue(string name);
        void SetBaseValue(string name, float value);
        void ModifyCurrent(string name, float delta);
        event AttributeChangedHandler OnAttributeChanged;
    }
}
