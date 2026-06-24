namespace Change.Runtime.UI
{
    public sealed class WindowLayerSortingOrderManager
    {
        private readonly int[] _counters;

        public WindowLayerSortingOrderManager()
        {
            _counters = new int[4];
        }

        public int AllocateSortingOrder(Change.Framework.UI.WindowLayer layer)
        {
            int index = (int)layer;
            int baseValue = GetBaseValue(layer);
            int order = baseValue + _counters[index];
            _counters[index]++;
            return order;
        }

        public void ResetLayerIfNeeded(Change.Framework.UI.WindowLayer layer)
        {
            int index = (int)layer;
            if (_counters[index] > 900)
            {
                _counters[index] = 0;
            }
        }

        public int GetBaseValue(Change.Framework.UI.WindowLayer layer)
        {
            return layer switch
            {
                Change.Framework.UI.WindowLayer.Bottom => 0,
                Change.Framework.UI.WindowLayer.Normal => 1000,
                Change.Framework.UI.WindowLayer.Popup => 2000,
                Change.Framework.UI.WindowLayer.Top => 3000,
                _ => 0,
            };
        }
    }
}
