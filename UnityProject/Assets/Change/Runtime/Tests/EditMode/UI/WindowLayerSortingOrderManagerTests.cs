using Change.Framework.UI;
using NUnit.Framework;

namespace Change.Runtime.UI.Tests
{
    public class WindowLayerSortingOrderManagerTests
    {
        [Test]
        public void GetBaseValue_Bottom_ReturnsZero()
        {
            var manager = new WindowLayerSortingOrderManager();
            Assert.AreEqual(0, manager.GetBaseValue(WindowLayer.Bottom));
        }

        [Test]
        public void GetBaseValue_Normal_Returns1000()
        {
            var manager = new WindowLayerSortingOrderManager();
            Assert.AreEqual(1000, manager.GetBaseValue(WindowLayer.Normal));
        }

        [Test]
        public void GetBaseValue_Popup_Returns2000()
        {
            var manager = new WindowLayerSortingOrderManager();
            Assert.AreEqual(2000, manager.GetBaseValue(WindowLayer.Popup));
        }

        [Test]
        public void GetBaseValue_Top_Returns3000()
        {
            var manager = new WindowLayerSortingOrderManager();
            Assert.AreEqual(3000, manager.GetBaseValue(WindowLayer.Top));
        }

        [Test]
        public void AllocateSortingOrder_FirstCall_ReturnsBaseValue()
        {
            var manager = new WindowLayerSortingOrderManager();
            var order = manager.AllocateSortingOrder(WindowLayer.Normal);
            Assert.AreEqual(1000, order);
        }

        [Test]
        public void AllocateSortingOrder_SecondCall_ReturnsBaseValuePlusOne()
        {
            var manager = new WindowLayerSortingOrderManager();
            manager.AllocateSortingOrder(WindowLayer.Normal);
            var order = manager.AllocateSortingOrder(WindowLayer.Normal);
            Assert.AreEqual(1001, order);
        }

        [Test]
        public void AllocateSortingOrder_MultipleCalls_IncrementsCounter()
        {
            var manager = new WindowLayerSortingOrderManager();
            manager.AllocateSortingOrder(WindowLayer.Normal); // 1000
            manager.AllocateSortingOrder(WindowLayer.Normal); // 1001
            var order = manager.AllocateSortingOrder(WindowLayer.Normal); // 1002
            Assert.AreEqual(1002, order);
        }

        [Test]
        public void AllocateSortingOrder_DifferentLayers_HaveIndependentCounters()
        {
            var manager = new WindowLayerSortingOrderManager();
            manager.AllocateSortingOrder(WindowLayer.Normal); // counter Normal = 1
            manager.AllocateSortingOrder(WindowLayer.Normal); // counter Normal = 2
            var popupOrder = manager.AllocateSortingOrder(WindowLayer.Popup); // counter Popup = 1
            Assert.AreEqual(2000, popupOrder);
        }

        [Test]
        public void AllocateSortingOrder_PopupLayer_UsesOwnBaseValue()
        {
            var manager = new WindowLayerSortingOrderManager();
            manager.AllocateSortingOrder(WindowLayer.Popup); // 2000
            var order = manager.AllocateSortingOrder(WindowLayer.Popup); // 2001
            Assert.AreEqual(2001, order);
        }

        [Test]
        public void AllocateSortingOrder_BottomLayer_UsesOwnBaseValue()
        {
            var manager = new WindowLayerSortingOrderManager();
            manager.AllocateSortingOrder(WindowLayer.Bottom); // 0
            var order = manager.AllocateSortingOrder(WindowLayer.Bottom); // 1
            Assert.AreEqual(1, order);
        }

        [Test]
        public void ResetLayerIfNeeded_WhenCounterIsLow_DoesNotReset()
        {
            var manager = new WindowLayerSortingOrderManager();
            manager.AllocateSortingOrder(WindowLayer.Normal); // counter = 1
            manager.ResetLayerIfNeeded(WindowLayer.Normal);
            var order = manager.AllocateSortingOrder(WindowLayer.Normal); // should be 1001, not 1000
            Assert.AreEqual(1001, order);
        }

        [Test]
        public void ResetLayerIfNeeded_WhenCounterExceeds900_ResetsToZero()
        {
            var manager = new WindowLayerSortingOrderManager();
            for (int i = 0; i < 901; i++)
            {
                manager.AllocateSortingOrder(WindowLayer.Normal);
            }
            // counter is now 901 (> 900)
            manager.ResetLayerIfNeeded(WindowLayer.Normal);
            var order = manager.AllocateSortingOrder(WindowLayer.Normal); // should be 1000 (baseValue + 0)
            Assert.AreEqual(1000, order);
        }

        [Test]
        public void ResetLayerIfNeeded_DoesNotAffectOtherLayers()
        {
            var manager = new WindowLayerSortingOrderManager();
            // Exhaust Normal layer to trigger reset
            for (int i = 0; i < 901; i++)
            {
                manager.AllocateSortingOrder(WindowLayer.Normal);
            }
            manager.AllocateSortingOrder(WindowLayer.Popup); // counter Popup = 1

            manager.ResetLayerIfNeeded(WindowLayer.Normal);

            // Popup should be unaffected
            var popupOrder = manager.AllocateSortingOrder(WindowLayer.Popup); // should be 2001
            Assert.AreEqual(2001, popupOrder);
        }

        [Test]
        public void AllocateSortingOrder_AfterReset_WrapsAround()
        {
            var manager = new WindowLayerSortingOrderManager();
            // Allocate up to 900, then next alloc pushes counter to 901
            for (int i = 0; i < 901; i++)
            {
                manager.AllocateSortingOrder(WindowLayer.Normal);
            }
            // counter is now 901
            manager.ResetLayerIfNeeded(WindowLayer.Normal);
            // After reset, counter is 0
            var firstAfterReset = manager.AllocateSortingOrder(WindowLayer.Normal); // 1000
            var secondAfterReset = manager.AllocateSortingOrder(WindowLayer.Normal); // 1001
            Assert.AreEqual(1000, firstAfterReset);
            Assert.AreEqual(1001, secondAfterReset);
        }

        [Test]
        public void ResetLayerIfNeeded_CounterOver900_ResetsToZero()
        {
            var manager = new WindowLayerSortingOrderManager();

            // 分配 901 次，触发计数器 > 900
            for (int i = 0; i < 901; i++)
            {
                manager.AllocateSortingOrder(WindowLayer.Normal);
            }

            manager.ResetLayerIfNeeded(WindowLayer.Normal);

            // 下次分配应该从 1000 开始（重置后，counter=0 → base+0=1000）
            int nextOrder = manager.AllocateSortingOrder(WindowLayer.Normal);
            Assert.AreEqual(1000, nextOrder);
        }

        [Test]
        public void ResetLayerIfNeeded_CounterUnder900_DoesNotReset()
        {
            var manager = new WindowLayerSortingOrderManager();

            // 分配 5 次（counter 变为 5）
            for (int i = 0; i < 5; i++)
            {
                manager.AllocateSortingOrder(WindowLayer.Normal);
            }

            manager.ResetLayerIfNeeded(WindowLayer.Normal);

            // 下次分配应该从 1005 开始（未重置，counter=5 → base+5=1005）
            int nextOrder = manager.AllocateSortingOrder(WindowLayer.Normal);
            Assert.AreEqual(1005, nextOrder);
        }
    }
}
