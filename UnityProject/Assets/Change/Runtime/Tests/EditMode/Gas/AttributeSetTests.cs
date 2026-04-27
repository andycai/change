using Change.Framework.Gas;
using Change.Runtime.Gas;
using NUnit.Framework;

namespace Change.Runtime.Tests.Gas
{
    public class AttributeSetTests
    {
        [Test]
        public void GetCurrentValue_ReturnsBaseValue_WhenNoModifiers()
        {
            var set = new AttributeSet();
            set.SetBaseValue("HP", 100f);
            Assert.AreEqual(100f, set.GetCurrentValue("HP"));
        }

        [Test]
        public void AddAdditive_IncreasesCurrentValue()
        {
            var set = new AttributeSet();
            set.SetBaseValue("ATK", 50f);
            var attr = set.GetAttribute("ATK");
            attr.AddAdditive(30f);
            // CurrentValue should automatically recalculate due to dirty flag
            Assert.AreEqual(80f, attr.CurrentValue);
        }

        [Test]
        public void AddMultiplicative_MultipliesAfterAdditive()
        {
            var set = new AttributeSet();
            set.SetBaseValue("ATK", 100f);
            var attr = set.GetAttribute("ATK");
            attr.AddAdditive(50f);
            attr.AddMultiplicative(1.5f);
            Assert.AreEqual(225f, attr.CurrentValue);
        }

        [Test]
        public void RemoveAdditive_DecreasesCurrentValue()
        {
            var set = new AttributeSet();
            set.SetBaseValue("ATK", 50f);
            var attr = set.GetAttribute("ATK");
            attr.AddAdditive(30f);
            Assert.AreEqual(80f, attr.CurrentValue);
            attr.RemoveAdditive(30f);
            Assert.AreEqual(50f, attr.CurrentValue);
        }

        [Test]
        public void OnAttributeChanged_FiresWhenBaseValueChanges()
        {
            var set = new AttributeSet();
            set.SetBaseValue("HP", 100f);
            string changedName = null;
            float oldVal = 0f, newVal = 0f;
            set.OnAttributeChanged += (name, old, @new) =>
            {
                changedName = name;
                oldVal = old;
                newVal = @new;
            };
            set.SetBaseValue("HP", 80f);
            Assert.AreEqual("HP", changedName);
            Assert.AreEqual(100f, oldVal);
            Assert.AreEqual(80f, newVal);
        }

        [Test]
        public void GetAttribute_UnknownName_ReturnsNull()
        {
            var set = new AttributeSet();
            Assert.IsNull(set.GetAttribute("Unknown"));
        }
    }
}
